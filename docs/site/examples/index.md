# Examples

End-to-end walkthroughs for CaeriusNet v12. Each page covers one category from the SQL Server objects to a C# repository using the DI-registered runtime, immutable `StoredProcedureCommand` descriptions and explicit result contracts.

::: info SQL Server stored-procedure examples
The examples use SQL Server stored procedures and TVPs. C# parameter builder calls use parameter identifiers without the SQL `@` prefix; the SQL snippets continue to use normal SQL Server syntax.
:::

| Page | Scope |
|---|---|
| [Stored procedures](/examples/stored-procedures) | Basic reads, writes, cache tiers, error handling, return-type variants |
| [table-valued parameters](/examples/tvp) | `tvp_int`, `tvp_guid`, composite-key TVPs, TVP combined with scalar writes |
| [Multi-result sets](/examples/multi-result-sets) | Two-set and three-set reads, including a TVP filter |
| [Transactions](/examples/transactions) | Commit, C#-side rollback, SQL-side rollback (`BEGIN CATCH`), poison handling |

## Conventions used in this section

- **Repositories** inject `ICaeriusNetDbContext` via primary-constructor DI (or `[FromKeyedServices]` for a named database) and expose intent-revealing async methods.
- **DTOs and TVPs** use `[GenerateDto]` / `[GenerateTvp]` — sealed partial records with primary constructors.
- **All examples** propagate `CancellationToken` and use `await using` for transaction scopes.
- **SQL snippets** use explicit schemas and `SET NOCOUNT ON`; all application calls remain stored-procedure calls.
- **Telemetry callouts** describe the stable database conventions and intentionally never expose connection strings, parameter values, cache keys or TVP values.

## Running a sample

Register the runtime with `services.AddCaeriusNet(options => options.UseSqlServer(connectionString))`, then adapt the SQL objects and repository snippets to your schema. For Redis, install `CaeriusNet.Redis`, register the shared multiplexer and add a generated or hand-written `ICaeriusCacheCodec<T>`. For Aspire, use `CaeriusNet.Aspire` so SQL/Redis client registration and health checks remain in the host.

The runnable applications live under [`examples/`](https://github.com/CaeriusNET/CaeriusNet/tree/main/examples). The snippets on this site use the same dependency-injection and stored-procedure contracts.

---

Pick a page from the table above to dive in.
