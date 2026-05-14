# Examples

End-to-end walkthroughs for CaeriusNet. Each page covers one category from the SQL Server objects to the C# repository, including the telemetry tags emitted along the way.

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

- **Repositories** inject `ICaeriusNetDbContext` via primary-constructor DI and expose intent-revealing async methods.
- **DTOs and TVPs** use `[GenerateDto]` / `[GenerateTvp]` — sealed partial records with primary constructors.
- **All examples** propagate `CancellationToken` and use `await using` for transaction scopes.
- **SQL snippets** are idempotent (`SET NOCOUNT ON`, explicit schema) and match the schema used by the runnable **Examples** projects in the repository (`Exemples/` path retained for compatibility).
- **Telemetry callouts** name the tags emitted by each scenario so you can validate them in the Aspire dashboard.

## Running the examples

The full schema and stored procedures are created by the `init.sql` script bundled with the runnable example projects:

- `Exemples/Default/CaeriusNet.Exemples.Default.Console/` — traditional connection-string setup
- `Exemples/Aspire/CaeriusNet.Exemples.Aspire.AppHost/` — cloud-native Aspire orchestration (SQL Server + Redis containers, `init.sql` applied automatically via `WithCreationScript`)

Both projects exercise the same `IUsersService` and demonstrate seven distinct scenarios, including caches, TVPs, multiple result sets, and the three transaction outcomes: commit, C#-side rollback, and SQL-side rollback.

---

Pick a page from the table above to dive in.
