---
title: SQL Server benchmarks
description: CaeriusNet v12 end-to-end SQL Server benchmark baseline requirements.
---

# SQL Server benchmarks

SQL Server measurements complement the CPU-only v12 BenchmarkDotNet suite. Run them against the supported matrix—SQL Server 2022 CU26 on Ubuntu 22.04 and SQL Server 2025 CU7 on Ubuntu 24.04—using the same stored-procedure schema and connection-pool configuration for before/after comparisons.

## Required release cases

The release baseline covers:

| Area | Cases |
| --- | --- |
| Mapping and scalars | DTO mapping, required/optional scalar conversion and null/error paths |
| TVP | empty, 1, 1 000 and large streaming inputs |
| Result sets | 1, 2, 5 and 10 sets for all three materialized collection families |
| Caching | memory/Frozen hit and miss behavior, plus typed Redis codec hit/miss |
| Command model | immutable descriptor validation and `SqlParameter` construction at execution |

Use real stored procedures only. v12 always sets `CommandType.StoredProcedure`; no benchmark should compare a runtime inline-SQL path because it is not a supported API.

Run this opt-in category only against a disposable database configured in `CAERIUS_BENCHMARK_SQL_CONNECTION`. The
benchmark identity provisions `dbo.caerius_benchmark_reader_{1,2,5,10}` and needs `CREATE OR ALTER PROCEDURE`; it does
not touch application procedures or log connection/parameter/result values. This category remains outside `all` and the
portable regression gate.

## Interpreting end-to-end results

Record the SQL Server image, host CPU, connection-pool state, data cardinality and result-shape capacities. Network and plan-cache effects commonly dominate a small client-side change. Compare allocation alongside latency, and investigate any repeatable regression above 5 percent before release.

The integration-test matrix verifies behavioral compatibility. Benchmark numbers are diagnostic baselines, not portable latency promises.
