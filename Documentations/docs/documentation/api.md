# API Reference

This page is a practical reference for the public API exposed by CaeriusNet. All examples target **C# 14 / .NET 10** with `Microsoft.Data.SqlClient`. Namespaces are abbreviated below — add the matching `using` directives in your project. C# 14 extension-block methods such as `QueryAsIEnumerableAsync`, `ExecuteNonQueryAsync`, and `BeginTransactionAsync` are available after importing their command namespaces.

> Examples assume DI is configured via `CaeriusNetBuilder` (see [Installation & Setup](/quickstart/getting-started)).
>
> Common command namespaces: `CaeriusNet.Commands.Reads`, `CaeriusNet.Commands.Writes`, and `CaeriusNet.Commands.Transactions`.
>
> CaeriusNet targets SQL Server stored procedures. Parameter names passed to builder methods are identifiers without the SQL `@` prefix.

## Builders

### `CaeriusNet.Builders.CaeriusNetBuilder`

Configures CaeriusNet services for DI.

```csharp
static CaeriusNetBuilder Create(IServiceCollection services);
static CaeriusNetBuilder Create(IHostApplicationBuilder builder);

CaeriusNetBuilder WithSqlServer(string connectionString);
CaeriusNetBuilder WithRedis(string? connectionString);

CaeriusNetBuilder WithAspireSqlServer(string connectionName = "sqlserver");
CaeriusNetBuilder WithAspireRedis(string connectionName = "redis");

CaeriusNetBuilder WithTelemetryOptions(CaeriusTelemetryOptions options);

IServiceCollection Build();
```

Example:

```csharp
CaeriusNetBuilder
    .Create(services)
    .WithSqlServer(configuration.GetConnectionString("Default")!)
    // .WithRedis("localhost:6379")
    .Build();
```

### `CaeriusNet.Builders.StoredProcedureParametersBuilder`

Fluent builder for Stored Procedure execution settings, parameters, and caching.

::: tip Parameter identifiers
Use the parameter name only when calling `AddParameter` or `AddTvpParameter`; do not include the SQL `@` prefix. This keeps C# call sites consistent while SQL definitions continue to use normal SQL Server parameter syntax.
:::

**Constructor:**

```csharp
StoredProcedureParametersBuilder(
    string schemaName,
    string procedureName,
    int    ResultSetCapacity = 16,
    int    CommandTimeout = 30);
```

**Parameter methods:**

```csharp
StoredProcedureParametersBuilder AddParameter(
    string parameter,
    object value,
    SqlDbType dbType);

StoredProcedureParametersBuilder AddTvpParameter<T>(
    string parameter,
    IEnumerable<T> items)
    where T : class, ITvpMapper<T>;
```

**Caching methods:**

```csharp
StoredProcedureParametersBuilder AddInMemoryCache(string cacheKey, TimeSpan expiration);
StoredProcedureParametersBuilder AddFrozenCache  (string cacheKey);
StoredProcedureParametersBuilder AddRedisCache   (string cacheKey, TimeSpan? expiration = null);
```

**Build:**

```csharp
StoredProcedureParameters Build();
```

Example with capacity, parameters, TVP, and Redis cache:

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Tvp_Ids_And_Age", 1024)
    .AddTvpParameter("Ids", tvpItems)                        // T : class, ITvpMapper<T>
    .AddParameter   ("Age", age, SqlDbType.Int)
    .AddRedisCache  ($"users:age:{age}", TimeSpan.FromMinutes(2))
    .Build();
```

## Telemetry

### `CaeriusNet.Telemetry.CaeriusTelemetryOptions`

Configuration record consumed globally by every command pipeline.

```csharp
public sealed class CaeriusTelemetryOptions
{
    public bool CaptureParameterValues { get; init; }
}
```

| Property | Default | Description |
|---|---|---|
| `CaptureParameterValues` | `false` | When `true`, the `caerius.sp.parameters` tag shows `@name=value`. TVP values always render as `[TVP]`. Off by default for PII safety. |

### `CaeriusNet.Telemetry.CaeriusDiagnostics`

Static registry of OpenTelemetry signals.

```csharp
public static class CaeriusDiagnostics
{
    public const  string         SourceName = "CaeriusNet";
    public static ActivitySource ActivitySource { get; }
    public static Meter          Meter          { get; }
}
```

Register in your telemetry pipeline (typically in `ServiceDefaults`):

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(CaeriusDiagnostics.SourceName))
    .WithMetrics(m => m.AddMeter (CaeriusDiagnostics.SourceName));
```

See [Aspire Integration — Tracing & Telemetry](/documentation/aspire#tracing-telemetry) for the complete tag and metric reference.

## Abstractions

### `CaeriusNet.Abstractions.ICaeriusNetDbContext`

Factory for opening SQL connections; exposes the optional Redis cache manager.

```csharp
IRedisCacheManager? RedisCacheManager { get; }
ValueTask<SqlConnection> DbConnectionAsync(CancellationToken ct = default);
```

### `CaeriusNet.Abstractions.ICaeriusNetTransaction`

Transaction scope obtained via `BeginTransactionAsync`. See [Transactions](/documentation/transactions) for the full contract.

```csharp
ValueTask CommitAsync   (CancellationToken ct = default);
ValueTask RollbackAsync (CancellationToken ct = default);
ValueTask DisposeAsync  ();

// Same Execute*/Query* surface as ICaeriusNetDbContext, enlisted in the transaction.
```

### `CaeriusNet.Abstractions.IRedisCacheManager`

Distributed cache adapter used when Redis is configured.

```csharp
bool TryGet<T>(string cacheKey, out T? value);
void Store<T> (string cacheKey, T value, TimeSpan? expiration) where T : notnull;
void Remove(string cacheKey);
```

## Mappers

### `CaeriusNet.Mappers.ISpMapper<T>`

Compile-time-friendly contract for mapping a row from `SqlDataReader`.

```csharp
public interface ISpMapper<TSelf> where TSelf : ISpMapper<TSelf>
{
    static abstract TSelf MapFromDataReader(SqlDataReader reader);
}
```

Manual implementation:

```csharp
public sealed record UserDto(int Id, string Name) : ISpMapper<UserDto>
{
    public static UserDto MapFromDataReader(SqlDataReader reader)
        => new(reader.GetInt32(0), reader.GetString(1));
}
```

### `CaeriusNet.Mappers.ITvpMapper<T>`

Defines how to convert items to `IEnumerable<SqlDataRecord>` for TVP transport.

```csharp
public interface ITvpMapper<TSelf> where TSelf : class, ITvpMapper<TSelf>
{
    static abstract string TvpTypeName { get; }
    IEnumerable<SqlDataRecord> MapAsSqlDataRecords(IEnumerable<TSelf> items);
}
```

Manual implementation:

```csharp
public sealed record UserIdsTvp(int Id) : ITvpMapper<UserIdsTvp>
{
    public static string TvpTypeName => "dbo.tvp_int";

    public IEnumerable<SqlDataRecord> MapAsSqlDataRecords(IEnumerable<UserIdsTvp> items)
    {
        var metaData = new[] { new SqlMetaData("Id", SqlDbType.Int) };
        var record   = new SqlDataRecord(metaData);

        foreach (var item in items)
        {
            record.SetInt32(0, item.Id);
            yield return record;
        }
    }
}
```

## Attributes (Source Generators)

### `CaeriusNet.Attributes.Dto.GenerateDtoAttribute`

Annotate a sealed partial record/class to generate `ISpMapper<T>` at compile time.

```csharp
[GenerateDto]
public sealed partial record UserDto(int Id, string Name, byte? Age);
```

### `CaeriusNet.Attributes.Tvp.GenerateTvpAttribute`

Annotate a sealed partial record/class to generate `ITvpMapper<T>`.

**Required init properties:**

```csharp
string Schema  { get; init; } = "dbo";
string TvpName { get; init; }
```

Example:

```csharp
[GenerateTvp(Schema = "Types", TvpName = "tvp_Int")]
public sealed partial record UsersIntTvp(int UserId);
```

See [Source Generators](/documentation/source-generators) for the complete generator behaviour and [Compiler Diagnostics](/documentation/diagnostics) for the analyzer rules.

## Read commands

Extension methods on `ICaeriusNetDbContext` for reading result sets.

Namespace: `CaeriusNet.Commands.Reads.SimpleReadSqlAsyncCommands`

```csharp
ValueTask<TResult?>
    FirstQueryAsync<TResult>(this ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);

ValueTask<ReadOnlyCollection<TResult>>
    QueryAsReadOnlyCollectionAsync<TResult>(this ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);

ValueTask<IEnumerable<TResult>?>
    QueryAsIEnumerableAsync<TResult>(this ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);

ValueTask<ImmutableArray<TResult>>
    QueryAsImmutableArrayAsync<TResult>(this ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);
```

`TResult` must be a class implementing `ISpMapper<TResult>`.

Example:

```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250).Build();
var users = await dbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct);
```

## Write commands

Extension methods on `ICaeriusNetDbContext` for non-query operations.

Namespace: `CaeriusNet.Commands.Writes.WriteSqlAsyncCommands`

```csharp
ValueTask<T?>  ExecuteScalarAsync<T>(this ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);
ValueTask<int> ExecuteNonQueryAsync (this ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);
ValueTask      ExecuteAsync         (this ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);
```

Examples:

```csharp
// Affected rows
var sp = new StoredProcedureParametersBuilder("dbo", "sp_UpdateUserAge_By_Guid")
    .AddParameter("Guid", guid, SqlDbType.UniqueIdentifier)
    .AddParameter("Age",  age,  SqlDbType.TinyInt)
    .Build();

var affected = await dbContext.ExecuteNonQueryAsync(sp, ct);

// Fire-and-forget
await dbContext.ExecuteAsync(sp, ct);
```

## Multiple result sets

Up to **5 result sets** per call. Each return type maps positionally to a `SELECT` statement in the SP.

Namespace: `CaeriusNet.Commands.Reads.MultiIEnumerableReadSqlAsyncCommands` (similar shapes exist for `ReadOnlyCollection` and `ImmutableArray`).

```csharp
Task<(IEnumerable<T1>, IEnumerable<T2>)>
    QueryMultipleIEnumerableAsync<T1, T2>(this ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);

Task<(IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>)>
    QueryMultipleIEnumerableAsync<T1, T2, T3>(/* ... */);

Task<(IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>, IEnumerable<T4>)>
    QueryMultipleIEnumerableAsync<T1, T2, T3, T4>(/* ... */);

Task<(IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>, IEnumerable<T4>, IEnumerable<T5>)>
    QueryMultipleIEnumerableAsync<T1, T2, T3, T4, T5>(/* ... */);
```

Example:

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_Get_Dashboard_Data", 128).Build();

var (users, orders, products) = await dbContext
    .QueryMultipleIEnumerableAsync<UserDto, OrderDto, ProductDto>(sp, ct);
```

## Transactions

Open a transaction scope from the DB context. See [Transactions](/documentation/transactions) for the complete state-machine and tracing reference.

```csharp
ValueTask<ICaeriusNetTransaction>
    BeginTransactionAsync(this ICaeriusNetDbContext, IsolationLevel level, CancellationToken ct = default);
```

Example:

```csharp
await using var tx = await dbContext.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
await tx.ExecuteNonQueryAsync(sp1, ct);
await tx.ExecuteNonQueryAsync(sp2, ct);
await tx.CommitAsync(ct);
```

## Caching

Caching is configured per call on `StoredProcedureParametersBuilder`:

- **Frozen** — in-process, immutable
- **InMemory** — in-process, expirable
- **Redis** — distributed (optional, requires `WithRedis(...)` or `WithAspireRedis(...)`)

Example:

```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddFrozenCache("users:all:frozen")
    .Build();

var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, ct);
```

See [Caching](/documentation/cache) for the full guide.

## Exceptions

### `CaeriusNet.Exceptions.CaeriusNetSqlException`

Wraps `SqlException` from any failed SQL command. The original exception is available via `InnerException`. Carries the originating procedure name to simplify diagnostics:

```csharp
try
{
    await dbContext.ExecuteAsync(sp, ct);
}
catch (CaeriusNetSqlException ex) when (ex.InnerException is SqlException sqlEx)
{
    logger.LogError(ex, "SP {Procedure} failed (error {Number})", ex.ProcedureName, sqlEx.Number);
}
```

The active OpenTelemetry span is tagged `ActivityStatusCode.Error` before the exception bubbles up.
