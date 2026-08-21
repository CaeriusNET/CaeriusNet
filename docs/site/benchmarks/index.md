---
title: Performance and benchmarks
description: CaeriusNet v12 BenchmarkDotNet methodology and release regression policy.
---

# Performance and benchmarks

CaeriusNet uses BenchmarkDotNet to establish v12 baselines for allocation-sensitive runtime paths. Results are comparative evidence, not a performance guarantee: repeat them on the target hardware, SQL Server image, schema and network path before drawing production conclusions.

| Page | Scope |
| --- | --- |
| [In-memory benchmarks](./in-memory) | command construction, mapping, scalars, collections and TVP row writers |
| [Collection benchmarks](./collections) | the three materialized result families for 1/2/5/10 sets |
| [Cache benchmarks](./cache) | generated typed binary codec encode/hit/miss behavior |
| [SQL Server benchmarks](./sql-server) | release matrix and end-to-end comparison requirements |

## Reproducible comparisons

Build once in Release, then run the exact same compiled artifacts for both the benchmark and package-acceptance stages. Capture the BenchmarkDotNet reports, machine and SQL Server details, and compare `Mean`, `Ratio` and `Allocated` rather than a single timing number.

A repeatable regression above 5 percent, or extra allocation on a hot path, requires an explanation or a correction before a v12 release. This is especially important for TVP writing, scalar conversion, multi-result materialization and Redis codecs.

## Running locally

```bash
dotnet run -c Release --project benchmarks/CaeriusNet.Benchmark.csproj -- --filter "*"
```

Use BenchmarkDotNet filters to run a narrower class, for example `*TvpBenchmarks*`. The normal local job includes warmup and repeated iterations; preserve the generated reports when comparing revisions.

The `sql-server` and `redis` categories are opt-in integration measurements and are excluded from `all` and the portable
performance gate. Set `CAERIUS_BENCHMARK_SQL_CONNECTION` for SQL Server and also
`CAERIUS_BENCHMARK_REDIS_CONNECTION` for Redis. Use disposable instances: the runner needs `CREATE OR ALTER PROCEDURE`
and creates only `dbo.caerius_benchmark_reader_{1,2,5,10}` and `dbo.caerius_benchmark_redis_reader`. Connection,
parameter and result values are never logged.
