# CaeriusNet

<p align="center">
  <a href="https://www.nuget.org/packages/CaeriusNet"><img src="https://img.shields.io/nuget/v/CaeriusNet?style=flat&logo=nuget" alt="NuGet version"></a>
  <a href="https://www.nuget.org/packages/CaeriusNet"><img src="https://img.shields.io/nuget/dt/CaeriusNet?style=flat" alt="NuGet downloads"></a>
  <img src="https://img.shields.io/badge/.NET%2010-512BD4.svg?style=flat&logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/C%23%2014-%23239120.svg?style=flat&logo=csharp&logoColor=white" alt="C# 14">
  <img src="https://img.shields.io/badge/license-MIT-blue.svg?style=flat" alt="MIT License">
</p>

High-performance micro-ORM for C# 14 / .NET 10 that executes SQL Server Stored Procedures, maps DTOs at compile-time,
passes Table-Valued Parameters, and caches results — all in a single package, zero reflection at runtime.

## Installation

```
dotnet add package CaeriusNet
```

## Prerequisites

- .NET 10 or later
- SQL Server 2019 or later

## Quick Start

### 1. Configure (Program.cs)

```csharp
// Standard
CaeriusNetBuilder.Create(services)
    .WithSqlServer("Server=.;Database=MyDb;Trusted_Connection=True;")
    .Build();

// .NET Aspire
CaeriusNetBuilder.Create(builder)
    .WithAspireSqlServer("CaeriusNet")
    .WithAspireRedis()
    .Build();
```

### 2. Define a DTO

**Source-generated (recommended):**

```csharp
[GenerateDto]
public sealed partial record ProductDto(int Id, string Name, decimal Price);
// Generates: ISpMapper<ProductDto> with MapFromDataReader at compile-time
```

**Manual:**

```csharp
public sealed record ProductDto(int Id, string Name, decimal Price) : ISpMapper<ProductDto>
{
    public static ProductDto MapFromDataReader(SqlDataReader reader)
        => new(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2));
}
```

### 3. Execute a Stored Procedure

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetProducts", capacity: 1)
    .AddParameter("CategoryId", categoryId, SqlDbType.Int)
    .Build();

ReadOnlyCollection<ProductDto> products =
    await dbContext.QueryAsReadOnlyCollectionAsync<ProductDto>(sp, ct);
```

## Table-Valued Parameters (TVP)

**Source-generated:**

```csharp
[GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
public sealed partial record IntTvp(int Value);
```

**Manual:**

```csharp
public sealed record OrderLineDto(int ProductId, int Qty) : ITvpMapper<OrderLineDto>
{
    public static string TvpTypeName => "dbo.tvp_OrderLine";

    public static IEnumerable<SqlDataRecord> MapAsSqlDataRecords(IEnumerable<OrderLineDto> items)
    {
        var meta = new[] { new SqlMetaData("ProductId", SqlDbType.Int), new SqlMetaData("Qty", SqlDbType.Int) };
        foreach (var item in items)
        {
            var record = new SqlDataRecord(meta);
            record.SetInt32(0, item.ProductId);
            record.SetInt32(1, item.Qty);
            yield return record;
        }
    }
}
```

**Usage:**

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_BulkInsert", capacity: 1)
    .AddTvpParameter("OrderLines", orderLines)
    .Build();

await dbContext.ExecuteNonQueryAsync(sp, ct);
```

## Caching

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetProducts", capacity: 2)
    .AddParameter("CategoryId", categoryId, SqlDbType.Int)
    .AddFrozenCache("products:all")                          // immutable, process-lifetime
    // .AddInMemoryCache("products:all", TimeSpan.FromMinutes(5))
    // .AddRedisCache("products:all", TimeSpan.FromMinutes(5))
    .Build();
```

### Cache invalidation

Inject the `ICaeriusNetCache` façade to invalidate entries from any service:

```csharp
public sealed class ProductsService(ICaeriusNetCache cache)
{
    public async ValueTask InvalidateProductAsync(int id, CancellationToken ct)
    {
        await cache.RemoveAsync($"products:{id}", ct);              // all tiers
        await cache.RemoveAsync("products:all", CacheType.Frozen, ct);
        // await cache.ClearAsync(CacheType.InMemory, ct);          // dangerous; use sparingly
    }
}
```

`ClearAsync(CacheType.Redis)` intentionally throws `NotSupportedException` — clearing a shared
distributed cache from a single service is almost never what you want.

To bound the in-memory tier, configure it explicitly at startup:

```csharp
services.AddCaeriusNet(b => b
    .WithSqlServerConnection(connectionString)
    .WithInMemoryCacheOptions(new MemoryCacheOptions { SizeLimit = 50_000 }));
```

When `SizeLimit` is set, every cached entry is sized as `1` so the limit acts as a **maximum entry
count**.

## Transactions

Stored-procedure transactions reuse a single `SqlConnection` for the whole scope and attach the
underlying `SqlTransaction` to every command. Caching is bypassed inside a transaction.

```csharp
await using var tx = await dbContext.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

var debit = new StoredProcedureParametersBuilder("dbo", "sp_DebitAccount", capacity: 2)
    .AddParameter("AccountId", fromId, SqlDbType.Int)
    .AddParameter("Amount", amount, SqlDbType.Decimal)
    .Build();

var credit = new StoredProcedureParametersBuilder("dbo", "sp_CreditAccount", capacity: 2)
    .AddParameter("AccountId", toId, SqlDbType.Int)
    .AddParameter("Amount", amount, SqlDbType.Decimal)
    .Build();

await tx.ExecuteNonQueryAsync(debit, ct);
await tx.ExecuteNonQueryAsync(credit, ct);

await tx.CommitAsync(ct);   // omit -> auto-rollback on dispose
```

**Design constraints (enforced at runtime):**

| Rule                         | Behavior                                                                               |
|------------------------------|----------------------------------------------------------------------------------------|
| **State machine**            | Active → Committed / RolledBack / Poisoned                                             |
| **Single in-flight command** | Concurrent commands throw `InvalidOperationException`                                  |
| **Cache bypass**             | No reads from cache, no writes to cache inside a transaction                           |
| **Poison state**             | A failing command poisons the scope — only `RollbackAsync`/`DisposeAsync` remain valid |
| **Auto-rollback**            | If `CommitAsync` is never called, the transaction rolls back on dispose                |
| **No nesting**               | `BeginTransactionAsync` on an active transaction throws `NotSupportedException`        |

## Write Operations

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_CreateProduct", capacity: 2)
    .AddParameter("Name", name, SqlDbType.NVarChar)
    .AddParameter("Price", price, SqlDbType.Decimal)
    .Build();

await dbContext.ExecuteNonQueryAsync(sp, ct);

// Or retrieve a scalar return value
int newId = await dbContext.ExecuteScalarAsync<int>(sp, ct);
```

## Multi-Result Sets

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetDashboard", capacity: 0).Build();

(IEnumerable<ProductDto> products, IEnumerable<CategoryDto> categories) =
    await dbContext.QueryMultipleIEnumerableAsync<ProductDto, CategoryDto>(sp, ct);
```

Supported up to 5 result sets: `QueryMultipleIEnumerableAsync<T1,T2>` through
`QueryMultipleIEnumerableAsync<T1,T2,T3,T4,T5>`.

## Available Query Methods

| Method                              | Returns                  |
|-------------------------------------|--------------------------|
| `FirstQueryAsync<T>`                | `T?` (first row or null) |
| `QueryAsReadOnlyCollectionAsync<T>` | `ReadOnlyCollection<T>`  |
| `QueryAsIEnumerableAsync<T>`        | `IEnumerable<T>`         |
| `QueryAsImmutableArrayAsync<T>`     | `ImmutableArray<T>`      |
| `ExecuteNonQueryAsync`              | `void`                   |
| `ExecuteAsync`                      | `void`                   |
| `ExecuteScalarAsync<T>`             | `T`                      |

## Supported DTO Types

The source generator maps C# types to SQL Server types and generates the correct `SqlDataReader` calls.

| C# Type          | SQL Server Type    | Reader Method           |
|------------------|--------------------|-------------------------|
| `bool`           | `bit`              | `GetBoolean`            |
| `byte`           | `tinyint`          | `GetByte`               |
| `short`          | `smallint`         | `GetInt16`              |
| `int`            | `int`              | `GetInt32`              |
| `long`           | `bigint`           | `GetInt64`              |
| `decimal`        | `decimal`          | `GetDecimal`            |
| `float`          | `real`             | `GetFloat`              |
| `Half`           | `real`             | `GetFloat` (cast)       |
| `double`         | `float`            | `GetDouble`             |
| `string`         | `nvarchar`         | `GetString`             |
| `char`           | `nchar`            | `GetString` (cast)      |
| `DateTime`       | `datetime2`        | `GetDateTime`           |
| `DateOnly`       | `date`             | `DateOnly.FromDateTime` |
| `TimeOnly`       | `time`             | `TimeOnly.FromTimeSpan` |
| `DateTimeOffset` | `datetimeoffset`   | `GetDateTimeOffset`     |
| `TimeSpan`       | `time`             | `GetTimeSpan`           |
| `Guid`           | `uniqueidentifier` | `GetGuid`               |
| `byte[]`         | `varbinary`        | `GetFieldValue<byte[]>` |
| Enums            | (underlying type)  | (underlying reader)     |

Types without a native mapping fall back to `sql_variant` with a compile-time warning (CAERIUS005/006).

## Observability

CaeriusNet uses `[LoggerMessage]` source-generated structured logging with zero-allocation event methods.

| Event Range | Category                              |
|-------------|---------------------------------------|
| 1xxx        | In-Memory cache operations            |
| 2xxx        | Frozen cache operations               |
| 3xxx        | Redis cache operations                |
| 4xxx        | Database / stored procedure execution |
| 5xxx        | Command execution lifecycle           |

All events include structured properties (cache key, schema, procedure name, elapsed time, row count) for integration
with OpenTelemetry, Seq, Application Insights, or any `ILogger` sink.

## Documentation

Full documentation, samples, and API reference: **[https://caerius.net](https://caerius.net)**

Source code & releases: **[https://github.com/CaeriusNET/CaeriusNet](https://github.com/CaeriusNET/CaeriusNet)**

## License

MIT