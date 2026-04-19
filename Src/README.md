# CaeriusNet

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

IReadOnlyCollection<ProductDto> products =
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
| `QueryAsReadOnlyCollectionAsync<T>` | `IReadOnlyCollection<T>` |
| `QueryAsIEnumerableAsync<T>`        | `IEnumerable<T>`         |
| `QueryAsImmutableArrayAsync<T>`     | `ImmutableArray<T>`      |
| `FirstQueryAsync<T>`                | `T` (first row)          |
| `ExecuteNonQueryAsync`              | `void`                   |
| `ExecuteAsync`                      | `void`                   |
| `ExecuteScalarAsync<T>`             | `T`                      |

## Documentation

Full documentation, samples, and API reference: **[https://caerius.net](https://caerius.net)**

Source code & releases: **[https://github.com/CaeriusNET/CaeriusNet](https://github.com/CaeriusNET/CaeriusNet)**

## License

MIT