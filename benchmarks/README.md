# CaeriusNet v12 benchmarks

The benchmark project measures the v12 hot paths rather than synthetic helper loops: immutable stored-procedure commands
and `SqlCommand` binding, generated TVP writers, scalar/reader materialization, 1/2/5/10 result-set metadata, cache and
Redis paths, binary DTO codecs, and GeneratorDriver cold/incremental execution. It targets .NET 10 and uses
BenchmarkDotNet 0.15.8 with full JSON reports and `MemoryDiagnoser`.

The `sql-server` and `redis` categories are opt-in integration benchmarks. `sql-server` requires a disposable instance
through `CAERIUS_BENCHMARK_SQL_CONNECTION`; `redis` also requires `CAERIUS_BENCHMARK_REDIS_CONNECTION`. They are
deliberately excluded from the default `all` suite and portable gate. The SQL connection needs `CREATE OR ALTER
PROCEDURE` rights on a disposable database: the runner creates only `dbo.caerius_benchmark_reader_{1,2,5,10}` and
`dbo.caerius_benchmark_redis_reader`, never logs parameter or result values.

## Run locally

Use a dry run first to validate setup, then a normal or CI-like run. BenchmarkDotNet arguments are passed after the
category.

```powershell
dotnet run --project benchmarks/CaeriusNet.Benchmark.csproj -c Release -- generator --job Dry --filter "*"
dotnet run --project benchmarks/CaeriusNet.Benchmark.csproj -c Release -- cache --filter "*Codec*"
dotnet run --project benchmarks/CaeriusNet.Benchmark.csproj -c Release -- all
```

Available categories are `all`, `command`, `tvp`, `materialization`, `multi-result`, `cache`, `generator`,
`sql-server`, and `redis`. Reports default to `BenchmarkDotNet.Artifacts/results`; set
`BENCHMARK_ARTIFACTS_PATH` to place them elsewhere. CI uses five warmups and 15 measurement iterations per job; a local
run uses three and 10. The runner exits non-zero for BenchmarkDotNet validation, build, execution, or missing-statistics
failures, so an empty JSON export cannot become a baseline.

## Baseline 11.2.0 and performance gate

There is intentionally no invented workstation baseline in the repository. The first v12 baseline must be measured by
the manual GitHub workflow on `ubuntu-24.04`, then reviewed and committed at
`benchmarks/baselines/11.2.0/` (no `v` prefix). A baseline directory contains only measured
`*-report-full*.json` files plus `caerius-benchmark-environment.v1.json`.

The environment manifest is written only after a successful BenchmarkDotNet run. It records checksums and measurement
counts for every JSON report, BenchmarkDotNet and SDK/runtime versions, OS and architecture, processor signature, runner
image, and the warmup/iteration/memory-diagnoser job settings. Its SHA-256 compatibility fingerprint covers the fields
that influence a measurement. The image version is retained for investigation but is not by itself a comparison axis.

The comparator verifies those report checksums before reading timings. It refuses to compare unlike fingerprints: it
creates a diagnostic Markdown report but does not label timing noise as a regression. This means a baseline recorded on
Windows, a different CPU, a different SDK/runtime/BDN, or a different benchmark job cannot silently govern the
GitHub-hosted CI gate.

In the manual **CaeriusNet benchmarks** workflow, use `mode=run` for an exploratory category. `mode=compare` and
`mode=record` require `category=all`, which prevents a subset from becoming a false full-suite baseline. `mode=record`
uploads an artifact containing the measured reports and manifest; review both and commit the artifact contents to
`benchmarks/baselines/11.2.0/`. A missing baseline is intentionally a failing bootstrap signal, not a zero-valued
baseline.

For a CI-run result set already present locally, the equivalent sequence is:

```powershell
$results = "<candidate-results-dir>"
dotnet run --project benchmarks/CaeriusNet.Benchmark.csproj -c Release -- --write-environment $results all
dotnet run --project benchmarks/CaeriusNet.Benchmark.csproj -c Release -- --record-baseline $results "<empty-baseline-dir>"
```

The gate compares a complete, environment-compatible identity set and fails when any of these hold:

- a benchmark or parameter combination is missing or newly introduced without a reviewed baseline;
- its **median** is more than 5% slower than the baseline;
- it allocates even one additional byte per operation;
- reports or manifest are malformed, incomplete, altered after recording, or captured from a non-comparable environment.

Run a comparison manually with:

```powershell
dotnet run --project benchmarks/CaeriusNet.Benchmark.csproj -c Release -- `
  --compare benchmarks/baselines/11.2.0 <candidate-results-dir> --max-time-regression 5
```

It writes `caerius-benchmark-comparison.md` next to the candidate results and exits non-zero on an environment mismatch
or performance/allocation regression. The pull-request workflow is the enforcement point; release runs retain the same
guard and publish the diagnostic artifacts.

## Reproducibility limits

GitHub-hosted runners do not guarantee identical CPU silicon, turbo state, or background load. A matching fingerprint is
a precondition for comparison, not a claim that microbenchmarks are perfectly deterministic. Investigate a failing gate
with the attached full JSON and comparison report, repeat on the same runner family when needed, and update the baseline
only through a reviewed, measured workflow artifact.
