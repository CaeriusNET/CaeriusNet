# CaeriusNet API breaks: v11.1.2 to v11.2.0

This is the consumer-facing record of intentional v12 changes. It is not a compatibility shim: v12 favors explicit SQL Server contracts, DI-owned runtime state and cache correctness over source compatibility.

| v11 pattern | v12 replacement | Migration note |
| --- | --- | --- |
| Global builder/runtime state | `AddCaeriusNet(...)` plus injected `ICaeriusNetDbContext` | Register named/keyed contexts for multiple databases. |
| Ad-hoc SQL or query-builder usage | `StoredProcedureCommandBuilder` | Runtime accepts `CommandType.StoredProcedure` only. |
| Mutable command objects | Immutable `StoredProcedureCommand` | Build once; descriptors include explicit SQL types/facets. |
| `Invalidate(cacheKeys)` | `DependsOn(tags)` / `Invalidates(tags)` | Tags describe logical data; cache keys are generated. |
| Raw `IEnumerable<T>` TVP input | `IReadOnlyCollection<T>` or `Func<CancellationToken, IEnumerable<T>>` | Collection is replayable/cacheable; factory streams and is not cacheable. |
| Arbitrary primary-constructor TVP class | Sealed positional record class or readonly positional record struct | Manual TVPs need `SqlServerTvpColumnAttribute` on every parameter. |
| Unsupported generated mapping becomes `sql_variant` | `CAERIUS005` error, no source | Choose a faithful SQL/CLR boundary. |
| AutoContracts v2 | Canonical AutoContracts v3 plus optional override | Run a new `Pull`; v2 is not silently accepted. |
| Raw MSBuild contract connection string | Named configuration, User Secrets, environment variable, or connection arguments | Direct CLI raw string is deprecated troubleshooting only. |
| VSTest logger/coverlet command line | Native Microsoft.Testing.Platform `--report-trx --coverage` | Use `dotnet test --project` / `--solution`. |

## Behavioral changes that need application handling

- Outputs, input/outputs and return values are read only after the reader is closed.
- `StreamAsync<T>` rejects commands with output parameters and is intentionally non-cacheable.
- Multi-result capacity `-1` means unspecified; explicit non-negative capacities override the command/default capacity.
- Rowless frames produced with `SET NOCOUNT OFF` are ignored between result sets; an empty `SELECT` remains a present result set.
- A transaction observes pre-cancellation before I/O without poisoning the slot, then uses full rollback after SQL error/cancellation.
- Cache invalidation after a durable write/commit runs without caller cancellation. `CaeriusCacheInvalidationException.SqlWasCommitted` indicates that retrying a non-idempotent procedure could duplicate the write.
- Redis uses versioned logical-tag snapshots and `caerius:v2` binary-hash keys. A stale read cannot republish after an invalidation when Redis is reachable; SQL Server and Redis are still not atomically committed during a Redis outage.

## Package and platform changes

- Consumer floor: SDK `10.0.400`, Roslyn 5.9+, .NET 10/C# 14.
- The published package contains analyzer and generator assemblies/PDBs under `analyzers/dotnet/cs`; the build-only multi-result generator is not shipped.
- No Native AOT compatibility marker is published for components that have not passed the package-only AOT smoke test.
- SQL Server 2025 native JSON/vector types need both `CaeriusSqlServer2025Types=Enabled` and a major-17 v3 manifest. They are not valid TVP columns in v12.

See the detailed [migration guide](MIGRATION-v11-v12.md) and the [documentation site](site/index.md).
