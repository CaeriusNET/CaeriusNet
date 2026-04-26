# Best Practices

This page distills practical recommendations for building reliable, secure, and high-performance applications with CaeriusNet. It complements the [Quickstart](/quickstart/getting-started), [Reading Data](/documentation/reading-data), [Writing Data](/documentation/writing-data), and [Advanced Usage](/documentation/advanced-usage) guides.

> Applies to: **C# 14 / .NET 10**, **SQL Server 2019 +**, `Microsoft.Data.SqlClient`.

## Architecture & patterns

- **Use the Repository pattern.** Keep data access isolated behind interfaces — `ICaeriusNetDbContext` is the only external dependency repositories should know about.
- **Inject `ICaeriusNetDbContext` via DI.** Never instantiate it manually; the lifetime and connection management is handled by the framework.
- **Favour sealed records.** They are immutable, lightweight, value-equatable, and pair well with the source generators.
- **Always prefer source generators.** The analyzer enforces the contract and the generated code is identical to a hand-written mapper:
  ```csharp
  [GenerateDto]
  public sealed partial record UserDto(int Id, string Name, byte? Age);

  [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
  public sealed partial record UserIdTvp(int Id);
  ```
- **Keep mapping out of services and controllers.** Repositories own SQL; services orchestrate them.

## DTO mapping

- Mapping is **ordinal-based** — the constructor parameter order **must** match the SP `SELECT` column order. Aliases are cosmetic.
- Mark columns that may return `NULL` as nullable in the DTO; the source generator emits `IsDBNull` guards automatically.
- Keep DTOs **purpose-specific**. Avoid catch-all DTOs that grow with every screen.
- Manual mapping example for reference:
  ```csharp
  public sealed record UserDto(int Id, string Name, byte? Age) : ISpMapper<UserDto>
  {
      public static UserDto MapFromDataReader(SqlDataReader reader)
          => new(
              reader.GetInt32(0),
              reader.GetString(1),
              reader.IsDBNull(2) ? null : reader.GetByte(2));
  }
  ```

## Stored Procedure conventions (T-SQL)

- **Use dedicated schemas** (`Users`, `Orders`, `Sales`, …) instead of the default `dbo` for application code where feasible.
- Always start procedures with `SET NOCOUNT ON;` to avoid spurious result sets.
- **Never `SELECT *`.** Explicitly list columns in the order your DTO expects.
- Keep result-set shape stable. Any change in cardinality or order requires a matching DTO change.
- Prefer parameterized procedures for all inputs — avoid dynamic SQL unless absolutely required.
- Wrap multi-statement writes in `BEGIN TRY / BEGIN CATCH` with explicit `COMMIT` / `ROLLBACK` and `THROW;` for re-raise.
- Adopt a consistent name convention, e.g. `Schema.sp_Action_Subject_By_Filter`.

## TVPs

- Define SQL types with the **minimal columns** needed.
- Use `[GenerateTvp]` for zero-boilerplate mappers.
- Ensure the .NET TVP record's primary-constructor parameters match the SQL column definition exactly (order, nullability, types).
- Pass TVPs `READONLY` (required by SQL Server).
- Validate non-empty input before calling — `AddTvpParameter` throws `ArgumentException` on empty collections.
- For very large sets, batch into reasonable chunks (e.g. 5 000–10 000 rows per call) to keep memory and CPU usage predictable.

## Caching strategy

Pick the right tier per call via `StoredProcedureParametersBuilder`:

- **Frozen** — in-process, immutable, fastest. **Only** for data that truly never changes during the application lifetime (lookup tables, currency codes, permission definitions).
- **InMemory** — in-process with TTL. Good for hot paths where some staleness is acceptable. Always set a sensible expiration.
- **Redis** — distributed. Use in multi-instance deployments. Secure with TLS + auth in production.

**Cache keys:**

- Deterministic, short, descriptive (e.g. `users:age:>=30`).
- Include all parameters that affect the result.
- Lowercase, colon-separated.

**Invalidation:**

- Prefer time-based expiry for mutable data.
- For Frozen, only cache truly immutable data — there is no invalidation hook.

See [Caching](/documentation/cache) for the full guide.

## Performance

- **Set `resultSetCapacity` accurately.** It pre-allocates the `List<T>` and avoids resize churn for large reads.
- **Return only required columns.** Less data = fewer allocations and faster TDS framing.
- **Pick the right collection.** `ImmutableArray<T>` for frozen / shared data; `ReadOnlyCollection<T>` for public APIs; `IEnumerable<T>` for LINQ pipelines.
- **Stream multi-result-sets** with the `QueryMultipleIEnumerableAsync` family rather than chaining separate calls.
- **Benchmark critical flows.** CaeriusNet's own [BenchmarkDotNet suites](/benchmarks/) are reproducible (`Random(42)`); use them as a baseline.

Internal performance levers worth knowing about:

| Mechanism | What it buys you |
|---|---|
| `SearchValues<char>` SIMD scans | Near-zero-cost parameter-name validation on modern CPUs |
| `FrozenDictionary` for Frozen cache | Lock-free concurrent reads |
| `GC.AllocateUninitializedArray` | Skips zero-fill on large result arrays that will be fully populated |
| `CollectionsMarshal.SetCount` + `AsSpan` | Populates `List<T>` in place without bounds checks per write |

## Transactions

- Use `await using` so `DisposeAsync` runs on every code path — including exceptions.
- Pass `CancellationToken` everywhere; it cancels in-flight SQL commands.
- **Keep transactions short.** They hold locks; long transactions block other readers and writers.
- **Don't perform non-database I/O** (HTTP calls, file writes, log shipping) inside a transaction scope.
- Use the **lowest isolation level** that meets your consistency requirements (`ReadCommitted` is the default).
- **Retry the entire transaction** at the caller level if the scope poisons — never attempt partial recovery.

```csharp
// ✅ Short, focused, cancellable
await using var tx = await dbContext
    .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

await tx.ExecuteNonQueryAsync(spDebit,  ct);
await tx.ExecuteNonQueryAsync(spCredit, ct);
await tx.CommitAsync(ct);
```

## Logging

- Configure `ILoggerFactory` **before** `CaeriusNetBuilder.Build()` — DI then wires the logger automatically.
- Filter by event-ID category to control verbosity per subsystem:
  - `CaeriusNet.Cache` → `Warning` in production
  - `CaeriusNet.Commands` → `Information` to keep timing visible
- Use **structured sinks** (Seq, Elasticsearch, OTLP) to leverage CaeriusNet's named placeholders (`{ProcedureName}`, `{Duration}`, `{RowCount}`).
- Set up alerts on event ID **5003** (slow execution) and **5004** (command failure) for production monitoring.

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddFilter("CaeriusNet.Cache",    LogLevel.Warning);
    logging.AddFilter("CaeriusNet.Commands", LogLevel.Information);
});
```

See [Logging & Observability](/documentation/logging) for the complete event-ID reference.

## Async, cancellation, reliability

- All APIs are asynchronous by design — never block on async calls (`.Result`, `.Wait()`).
- Propagate `CancellationToken` from the request boundary down to every database call.
- Configure reasonable command and connection timeouts in your connection string.
- The library opens, uses, and disposes connections automatically — do not manage `SqlConnection` instances manually.

## Error handling

| Issue | Likely cause | Fix |
|---|---|---|
| `InvalidCastException` at runtime | Reader method or DTO type doesn't match SQL column type | Align the `Get*` call (or DTO field type) with the actual SQL type |
| `IndexOutOfRangeException` | DTO has more parameters than the SP returns columns | Re-check `SELECT` arity and DTO parameter count |
| Aspire connection failure | `WithAspireSqlServer("name")` does not match AppHost name | Cross-check the resource name in the AppHost |
| TVP type mismatch | Schema/TvpName diverge between SQL and .NET, or column order is wrong | Re-align `[GenerateTvp]` arguments and constructor params |
| Cache miss when hit was expected | Different keys per call | Build keys deterministically from inputs |
| Memory growth (Frozen) | Frozen used for non-static data | Switch to InMemory with TTL |

## Security

- Use Stored Procedures with parameters — no string concatenation, no dynamic SQL.
- **Never cache sensitive data** (passwords, tokens, raw PII) without explicit threat-modelling.
- Secure Redis with TLS and authentication; restrict network access.
- Grant the application user the **minimum required SQL permissions** (`EXECUTE` on the SP schema, no `dbo`).

## Versioning & migrations

- Treat Stored Procedures and DTOs as a contract — version them when breaking changes are needed (e.g. `sp_GetUsers` → `sp_GetUsers_v2`).
- Add new procedures alongside existing ones; deprecate old ones once consumers migrate.

## Quick reference

```csharp
// Read with caching
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddInMemoryCache("users:all", TimeSpan.FromMinutes(2))
    .Build();

var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, ct);
```

```csharp
// Update with affected rows
var sp = new StoredProcedureParametersBuilder("dbo", "sp_UpdateUserAge_By_Guid")
    .AddParameter("Guid", guid, SqlDbType.UniqueIdentifier)
    .AddParameter("Age",  age,  SqlDbType.TinyInt)
    .Build();

var rows = await dbContext.ExecuteNonQueryAsync(sp, ct);
```

```csharp
// TVP read
var tvp = userIds.Select(id => new UserIdTvp(id));
var sp  = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Tvp_Ids", 256)
    .AddTvpParameter("Ids", tvp)
    .Build();

var users = await dbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct);
```

---

Use this page as a checklist when designing new queries or auditing existing ones. For deeper APIs and patterns, see [API Reference](/documentation/api), [Advanced Usage](/documentation/advanced-usage), and the [Examples](/examples/).
