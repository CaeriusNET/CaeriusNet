# Best practices

These recommendations apply to CaeriusNet 12 on C# 14, .NET 10 and SQL Server 2022/2025.

1. Use `SET NOCOUNT ON` in procedures, especially when they return multiple sets.
2. Treat SQL parameter metadata as a contract: supply `SqlDbType`, and add size, precision and scale when needed. Do not use `AddWithValue`.
3. Choose cardinality explicitly: `QuerySingle...` for unique rows; `QueryFirst...` only when later rows are intentionally irrelevant.
4. Use a materialized collection by default. Reach for `StreamAsync` only when its reader lifetime is intentional and one set is returned.
5. Put the common data-access contract in generated DTO/TVP types; keep manual mappers small and ordinal based.
6. Model a TVP as a SQL user-defined type and verify column order/type. Test empty, one-row and large inputs.
7. Cache only deterministic, authorization-safe reads. Redis requires a typed codec; never build cache keys at call sites.
8. Use Frozen cache for complete snapshots, not lazy refills. Give memory cache a bounded size and expiration.
9. Put invalidations on successful writes. Let transactions defer them until commit.
10. Keep a transaction short and single-threaded. Do not run concurrent commands through it.
11. Keep tracing free of parameter values and secrets; development capture needs an allowlist plus redaction.
12. Install AutoContracts separately and run `Verify` in CI after reviewing `Pull` output.

The most useful acceptance tests cover cardinality, `DBNull`, outputs, empty TVPs, result sets 2–10, cache corruption, transaction rollback and SQL Server 2022/2025.
