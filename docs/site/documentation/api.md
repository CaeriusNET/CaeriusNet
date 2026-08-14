# API reference

This reference covers the v12 public surface. All runtime APIs target `net10.0`, C# 14 and SQL Server 2022/2025.

## Registration

```csharp
services.AddCaeriusNet(options => options.UseSqlServer(connectionString));
services.AddCaeriusNet("reporting", options => options.UseSqlServer(reportingConnectionString));
```

The first call registers a scoped default `ICaeriusNetDbContext`; the second registers a keyed context. `AddCaeriusNet` is idempotent. Runtime settings, cache managers, loggers and connection factories are all resolved from DI.

`CaeriusNetOptions` has `UseSqlServer(string)`, `UseSqlServer(Func<IServiceProvider, SqlConnection>)`, `UseSqlServerLease(...)`, `ApplicationNamespace`, `CacheDatabaseIdentity`, and guarded development parameter-capture options. The string overload derives a non-secret identity from the SQL endpoint and catalog. A factory/lease configuration that uses Redis must set `CacheDatabaseIdentity` explicitly; the runtime rejects connection-string-shaped identities and never emits the identity in a cache key.

## Command construction

```csharp
var command = new StoredProcedureCommandBuilder("dbo", "usp_Product_Get", commandTimeout: 30)
    .AddParameter("Id", id, SqlDbType.Int)
    .AddOutputParameter("Status", SqlDbType.Int)
    .AddReturnValue()
    .WithResultSetCapacities(16)
    .Build();
```

| Member | Purpose |
| --- | --- |
| `AddParameter` | Input, output, input/output or return parameter with SQL facets. |
| `AddOutputParameter` / `AddInputOutputParameter` / `AddReturnValue` | Explicit output directions. |
| `AddTvpParameter<T>` | Structured input where `T : ITvpMapper<T>`. |
| `WithResultSetCapacities` | One to ten expected capacities. |
| `WithContractHash` | Explicit generated-contract identity. |
| `UseInMemoryCache` / `UseFrozenCache` / `UseRedisCache` | One cache policy for a read. |
| `Invalidate` | Cache invalidations to run after accepted write/commit. |

`StoredProcedureCommand` is immutable. Its parameters are materialized as `SqlParameter` only when a command is created, and always have `CommandType.StoredProcedure`.

## Mapping contracts

```csharp
public interface ISpMapper<out T>
{
    static abstract T MapFromDataReader(SqlDataReader reader);
}

public interface ITvpMapper<T>
{
    static abstract string SqlTypeName { get; }
    static abstract ImmutableArray<SqlMetaData> Metadata { get; }
    static abstract SqlDataRecord CreateRecord();
    static abstract void WriteRow(SqlDataRecord record, T row);
}
```

`[GenerateDto]` and `[GenerateTvp]` generate these contracts. Generated procedure types use `StoredProcedureCommandBuilder<TProcedure>` plus the static `ICaeriusGeneratedProcedure<T>` contracts.

## Context execution

All methods below are extensions on `ICaeriusNetDbContext` and accept a `StoredProcedureCommand` followed by an optional `CancellationToken`.

| Method | Result |
| --- | --- |
| `QueryFirstAsync<T>` / `QueryFirstOrDefaultAsync<T>` | Required or optional first row. |
| `QuerySingleAsync<T>` / `QuerySingleOrDefaultAsync<T>` | Exactly-one or zero-or-one row. |
| `QueryAsync<T>` | Materialized `IEnumerable<T>`. |
| `QueryReadOnlyCollectionAsync<T>` | Materialized `ReadOnlyCollection<T>`. |
| `QueryImmutableArrayAsync<T>` | Materialized `ImmutableArray<T>`. |
| `StreamAsync<T>` | Reader-backed `IAsyncEnumerable<T>` for one set, with no cache. |
| `ExecuteScalarAsync<T>` / `ExecuteScalarOrDefaultAsync<T>` | Required or defaultable scalar. |
| `ExecuteNonQueryAsync` / `ExecuteAsync` | Affected-row count or awaited no-result command. |
| `ExecuteWithOutputsAsync` | `CaeriusCommandResult<CaeriusOutputValues>`. |
| `ExecuteNonQueryWithOutputsAsync` | `CaeriusExecutionResult<int, CaeriusOutputValues>`. |

`QueryMultipleAsync`, `QueryMultipleReadOnlyCollectionAsync` and `QueryMultipleImmutableArrayAsync` are supplied for every arity from 2 to 10. They accept a per-result-set capacity and `CaeriusResultSetCountPolicy`, whose default is `Exactly`.

## Transactions

```csharp
await using var transaction = await database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
await transaction.ExecuteAsync(command, ct);
await transaction.CommitAsync(ct);
```

`ICaeriusNetTransaction` has `IsActive`, `CommitAsync` and `RollbackAsync`. Transaction extensions mirror context reads/writes where their connection lifetime is safe.

## Caching

`IInMemoryCacheManager` provides dynamic TTL/sliding entries and local single-flight, including a stateful `GetOrCreateAsync<T, TState>` overload for a static factory call site with no closure or boxed state. `IFrozenCacheManager` atomically replaces immutable snapshots. `ICaeriusRemoteCacheManager` is implemented by `IRedisCacheManager` from `CaeriusNet.Redis`; its runtime read-through is also single-flight per canonical key within a host.

```csharp
var multiplexer = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
services.AddSingleton<IConnectionMultiplexer>(multiplexer);
services.AddCaeriusCacheCodec<Order, OrderCacheCodec>();
services.AddCaeriusNetRedis();
```

The codec contract is `ICaeriusCacheCodec<T>` with `CodecId`, `ContractHash`, `Encode` and `Decode`. Generated DTOs supply codecs and registration helpers.

## Telemetry and exceptions

`CaeriusTelemetry.ActivitySourceName` and `CaeriusTelemetry.MeterName` are public constants; the instrumentation instances are private. The stable duration metric is `db.client.operation.duration` in seconds.

| Type | Meaning |
| --- | --- |
| `CaeriusNetSqlException` | SQL Server failure, with procedure, number, state, class and client connection ID. |
| `CaeriusContractException` | Cardinality, schema or result-set contract failure. |
| `CaeriusMappingException` | Mapper failure, including result-set and row index. |
| `CaeriusCacheException` | Strict cache mutation failure. |
| `OperationCanceledException` | Cancellation; not wrapped. |

See [reading data](/documentation/reading-data), [caching](/documentation/cache), [transactions](/documentation/transactions) and [Aspire integration](/documentation/aspire) for usage examples.
