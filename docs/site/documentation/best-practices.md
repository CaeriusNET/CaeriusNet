# Best practices

These recommendations apply to CaeriusNet 12 on C# 14, .NET 10 and SQL Server 2022/2025.

1. Use `SET NOCOUNT ON` in procedures, especially when they return multiple sets.
2. Treat SQL parameter metadata as a contract: supply `SqlDbType`, and add size, precision and scale when needed. Do not use `AddWithValue`.
3. Choose cardinality explicitly: `QuerySingle...` for unique rows; `QueryFirst...` only when later rows are intentionally irrelevant.
4. Use a materialized collection by default. Reach for `StreamAsync` only when its reader lifetime is intentional and one set is returned.
5. Put the common data-access contract in generated DTO/TVP types; keep manual mappers small and ordinal based.
6. Model a TVP as a SQL user-defined type and verify column order/type. Use a replayable `IReadOnlyCollection<T>` or explicit streaming factory; manual TVPs need every `SqlServerTvpColumnAttribute` facet. Test empty, one-row and large inputs.
7. Cache only deterministic, authorization-safe reads. Redis requires a typed codec; never build cache keys at call sites. Use `DependsOn`/`Invalidates` logical tags, not literal invalidation keys.
8. Use Frozen cache for complete snapshots, not lazy refills. Give memory cache a bounded size and expiration.
9. Put invalidations on successful writes. Let transactions defer them until commit; if `CaeriusCacheInvalidationException.SqlWasCommitted` is true, reconcile rather than retrying blindly.
10. Keep a transaction short and single-threaded. Do not run concurrent commands through it.
11. Keep tracing free of parameter values and secrets; development capture needs an allowlist plus redaction.
12. Install AutoContracts separately and run `Verify` in CI after reviewing a freshly pulled v3 manifest. Opt into native SQL Server 2025 JSON/vector only with a major-17 v3 manifest.

The most useful acceptance tests cover cardinality, `DBNull`, outputs, empty TVPs, result sets 2–10, cache corruption, transaction rollback and SQL Server 2022/2025.
