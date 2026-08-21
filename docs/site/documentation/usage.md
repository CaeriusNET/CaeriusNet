# Usage overview

CaeriusNet 12 is composed from four packages. The runtime only executes stored procedures; optional packages add integration rather than hidden global configuration.

| Need | Package | First API |
| --- | --- | --- |
| SQL runtime, mapping, local cache, transaction, telemetry | `CaeriusNet` | `services.AddCaeriusNet(...)` |
| Typed asynchronous Redis cache | `CaeriusNet.Redis` | `services.AddCaeriusNetRedis(...)` |
| Aspire clients, health check and OpenTelemetry wiring | `CaeriusNet.Aspire` | `builder.AddCaeriusNetAspire(...)` |
| Metadata discovery and contract manifests | `CaeriusNet.Contracts` | `CaeriusContractsMode=Pull` or `Verify` |

## Typical workflow

1. Register a default or keyed `ICaeriusNetDbContext` with `AddCaeriusNet`.
2. Define a result mapper or use `[GenerateDto]`; define a TVP writer or use `[GenerateTvp]`.
3. Build an immutable `StoredProcedureCommand` with `StoredProcedureCommandBuilder`.
4. Select an explicit cardinality, collection or streaming API.
5. Add a cache policy only to safe reads; declare `DependsOn(...)` tags on reads and matching `Invalidates(...)` tags on successful writes.

```csharp
var command = new StoredProcedureCommandBuilder("dbo", "usp_User_ByAge")
    .AddParameter("Age", age, SqlDbType.Int)
    .DependsOn("user")
    .UseInMemoryCache(TimeSpan.FromMinutes(1))
    .Build();

var users = await database.QueryImmutableArrayAsync<User>(command, cancellationToken: ct);
```

## Runtime API groups

| Group | APIs |
| --- | --- |
| Cardinality | `QueryFirstAsync`, `QueryFirstOrDefaultAsync`, `QuerySingleAsync`, `QuerySingleOrDefaultAsync` |
| Materialized results | `QueryAsync`, `QueryReadOnlyCollectionAsync`, `QueryImmutableArrayAsync` |
| Streaming | `StreamAsync` for one result set, no cache and no output parameters |
| Scalars and writes | `ExecuteScalarAsync`, `ExecuteScalarOrDefaultAsync`, `ExecuteAsync`, `ExecuteNonQueryAsync` |
| Outputs | `ExecuteWithOutputsAsync`, `ExecuteNonQueryWithOutputsAsync` |
| Transactions | `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync` |
| Result sets | `QueryMultipleAsync`, `QueryMultipleReadOnlyCollectionAsync`, `QueryMultipleImmutableArrayAsync` (arities 2–10) |

The default multiple-result contract requires exactly the announced number of sets. `CaeriusResultSetCountPolicy` is an explicit escape hatch for genuinely variable procedures, not a convenience for mismatched SQL.

See [reading data](/documentation/reading-data), [TVPs](/documentation/tvp), [caching](/documentation/cache), [transactions](/documentation/transactions), [Aspire](/documentation/aspire) and [AutoContracts](/documentation/auto-contracts).
