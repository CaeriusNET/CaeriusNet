# Best Practices and Guidelines

This guide distills practical recommendations for building reliable, secure, and high‑performance applications with Caerius.NET. It complements the Quickstart, Usage, and Advanced Usage chapters.

Applies to: C# 13 / .NET 10, SQL Server 2019+, Microsoft.Data.SqlClient.

## Architecture and Patterns

- Prefer the Repository pattern. Keep data access isolated in repositories behind interfaces.
- Use Dependency Injection to obtain ICaeriusNetDbContext where needed.
- Favor sealed records for DTOs and TVPs. They are immutable, lightweight, and great with source generators.
- Prefer source generators:
```csharp
  [GenerateDto] // to auto-generate ISpMapper<T> for DTOs.
  [GenerateTvp(Schema = "", TvpName = "")] // to auto-generate ITvpMapper<T> for TVPs.
```  
- Keep mapping and database concerns out of controllers/services. Services orchestrate repositories.

## DTO Mapping Guidelines

- Mapping is ordinal-based. The constructor parameter order of your DTO must match the column order in the SQL result set.
  - Column names don’t affect mapping. Aliases can improve readability but are not required for mapping.
- Use nullable types for columns that may return NULL from SQL Server.
- Keep DTOs minimal and purpose‑specific. Avoid large catch‑all DTOs.
- Example (manual mapping):
```csharp
public sealed record UserDto(int Id, string Name, byte? Age)
    : ISpMapper<UserDto>
{
    public static UserDto MapFromDataReader(SqlDataReader reader)
        => new(reader.GetInt32(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetByte(2));
}
```

## Stored Procedure Guidelines (T/SQL)

- Use dedicated schemas (e.g., App, Users, Sales). Avoid dbo for application code where feasible.
- Always SET NOCOUNT ON inside procedures to avoid extra result sets.
- Keep result shapes stable. Any change in cardinality or order requires a matching DTO change.
- Avoid SELECT *. Explicitly list columns in the exact order your DTO expects.
- Prefer parameterized procedures for all inputs. Avoid dynamic SQL unless absolutely required.
- Use TRY/CATCH with explicit COMMIT/ROLLBACK for transactional procedures when necessary.
- Keep procedure names task‑based and consistent: schema.sp_Action_Subject_By_Filter.

## TVP (Table‑Valued Parameter) Guidance

- Create SQL types with the minimal necessary columns and indexes where relevant.
- Use [GenerateTvp] when possible. It generates TvpTypeName and mapping boilerplate for you.
- Ensure the .NET TVP columns (constructor parameters) match the SQL TVP type definition exactly.
- Keep TVP payload sizes reasonable. Extremely large TVPs can increase CPU and memory usage on both ends.
- Pass TVPs read‑only (as required by SQL Server) and consider batching if sets are very large.

## Caching Strategy

Choose the right cache per call via StoredProcedureParametersBuilder:

- Frozen cache
  - In‑process, immutable, fastest.
  - Use only for static reference data that rarely/never changes (e.g., lookup tables).
  - No expiration, cleared when the process restarts.
- In‑memory cache
  - In‑process with expiration. Good for hot paths where staleness is acceptable.
  - Always set a sensible expiration.
- Redis cache
  - Distributed, optional. Use in multi‑instance deployments for shared caching.
  - Secure with TLS and auth. Set expirations aligned with your invalidation strategy.

Cache key design:
- Deterministic, short, and descriptive (e.g., users:age:>=30).
- Include input parameters and stable identifiers.
- Prefer lowercase and colon separators.

Invalidation:
- Prefer time‑based expiry for mutable data.
- For Frozen cache, only cache immutable data to avoid invalidation complexity.

## Performance Tips

- Set ResultSetCapacity accurately to minimize list/array resizing.
- Return only required columns. Avoid over‑fetching.
- Avoid unnecessary allocations in hot paths; use ReadOnlyCollection/ImmutableArray variants where appropriate.
- Benchmark critical flows. Keep an eye on memory pressure for Frozen and In‑Memory caches.
- For very large multi‑result queries, use the MultiIEnumerable APIs to stream sets efficiently.

## Async, Cancellation, and Reliability

- All APIs are asynchronous by design. Don’t block on async calls.
- Propagate CancellationToken from the request boundary to database calls.
- Configure reasonable command/connection timeouts in connection strings or command options.
- Open one connection per operation via ICaeriusNetDbContext.DbConnection(), and dispose it promptly (handled by library helpers).

## Error Handling and Troubleshooting

Common issues and resolutions:

- Connection issues (Aspire)
  - Ensure WithAspireSqlServer("name") matches your AppHost AddSqlServer/AddDatabase name.
  - Confirm the connection string is available at runtime (AppHost injection).
- TVP type mismatch
  - Verify schema and type name (Schema.TvpName) match between SQL and .NET.
  - Ensure constructor parameters and SQL TVP columns align (order and types).
- Mapping errors
  - InvalidCastException typically indicates an ordinal/type mismatch. Check DTO constructor order and SQL SELECT order.
- Cache misses
  - Verify identical cache keys and parameters across calls. For Redis, check connectivity and configuration.
- Memory growth
  - Frozen cache is immutable and monotonic. Use only for true constants.
  - Tune In‑Memory cache expiration and avoid caching very large payloads unnecessarily.

## Security Considerations

- Use stored procedures with parameters to avoid SQL injection.
- Do not cache sensitive data unless necessary and lawful. Prefer short expirations and encryption at rest/in‑transit.
- Secure Redis with TLS and authentication; restrict network access to trusted environments.
- Limit SQL permissions for the application user to the minimum required.

## Versioning and Migrations

- Treat stored procedures and DTOs as a contract. Version them when breaking changes are needed.
- Add new procedures and DTOs alongside existing ones during migrations, then deprecate old versions.

## Examples

- Read with caching (In‑memory):
```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddInMemoryCache("users:all", TimeSpan.FromMinutes(2))
    .Build();
var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, cancellationToken);
```

- Write with affected rows:
```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_UpdateUserAge_By_Guid")
    .AddParameter("Guid", guid, SqlDbType.UniqueIdentifier)
    .AddParameter("Age", age, SqlDbType.TinyInt)
    .Build();
int rows = await dbContext.ExecuteNonQueryAsync(sp, cancellationToken);
```

- TVP example:
```csharp
var tvpItems = new List<UsersIdsTvp> { new(1), new(2), new(3) };
var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Tvp_Ids", 256)
    .AddTvpParameter("Ids", tvpItems)
    .Build();
var users = await dbContext.QueryAsIEnumerableAsync<UserDto>(sp, cancellationToken);
```

---

Use this page as a checklist when designing new queries or optimizing existing ones. For detailed APIs and examples, see Usage, Advanced Usage, Caching, and API Reference.