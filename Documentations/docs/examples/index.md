# Examples

End-to-end, production-shaped walkthroughs for CaeriusNet. Each page covers **one category** from the SQL Server objects to the C# repository, including the telemetry tags emitted along the way.

| Page | Scope |
|---|---|
| [Stored Procedures](/examples/stored-procedures) | Basic reads, writes, cache tiers, error handling, return-type variants |
| [Table-Valued Parameters](/examples/tvp) | `tvp_int`, `tvp_guid`, composite-key TVPs, TVP combined with scalar writes |
| [Multi-Result Sets](/examples/multi-result-sets) | 2-set / 3-set reads, TVP + multi-RS in one round-trip |
| [Transactions](/examples/transactions) | Commit, C#-side rollback, SQL-side rollback (`BEGIN CATCH`), poison handling |

## Conventions used in this section

- **Repositories** inject `ICaeriusNetDbContext` via primary-constructor DI and expose intent-revealing async methods.
- **DTOs and TVPs** use `[GenerateDto]` / `[GenerateTvp]` — sealed partial records with primary constructors.
- **All examples** propagate `CancellationToken` and use `await using` for transaction scopes.
- **SQL snippets** are idempotent (`SET NOCOUNT ON`, explicit schema) and match the schema used by the runnable `Exemples/` projects in the repository.
- **Telemetry callouts** name the tags emitted by each scenario so you can validate them in the Aspire dashboard.

## Running the examples

The full schema and Stored Procedures are created by the `init.sql` script bundled with the runnable example projects:

- `Exemples/Default/CaeriusNet.Exemples.Default.Console/` — traditional connection-string setup
- `Exemples/Aspire/CaeriusNet.Exemples.Aspire.AppHost/` — cloud-native Aspire orchestration (SQL Server + Redis containers, `init.sql` applied automatically via `WithCreationScript`)

Both projects exercise the same `IUsersService` and demonstrate seven distinct scenarios — including caches, TVPs, multi-result-sets, and the three transaction outcomes (commit, C#-side rollback, SQL-side rollback).

---

Pick a page from the table above to dive in.
