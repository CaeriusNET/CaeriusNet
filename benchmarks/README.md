# CaeriusNet v12 benchmarks

The benchmark project measures v12 hot paths without requiring a SQL Server instance: immutable stored-procedure
commands, canonical parameter keys, source-generated TVP row writers (including an empty TVP), materialization of
1/2/5/10 result sets, injected memory/Frozen cache hits and read-through misses, and the source-generated binary DTO
codec. It targets .NET 10 and writes BenchmarkDotNet full JSON reports that are used by the CI performance gate.

## Run locally

```powershell
dotnet run --project benchmarks/CaeriusNet.Benchmark.csproj -c Release -- all
```

The available categories are `all`, `command`, `tvp`, `materialization`, and `cache`. BenchmarkDotNet arguments are
passed through after the category; for example, the following runs only the cache codec methods:

```powershell
dotnet run --project benchmarks/CaeriusNet.Benchmark.csproj -c Release -- cache --filter *Codec*
```

Reports default to `BenchmarkDotNet.Artifacts/results`. Set `BENCHMARK_ARTIFACTS_PATH` to place them elsewhere. The CI
configuration uses 5 warmups and 15 measurements per job and never uses the `Dry` job. The runner exits non-zero when
BenchmarkDotNet has a build, validation, execution, or missing-statistics failure, so an empty JSON export can never be
recorded or compared as a baseline.

## Baseline workflow and gate

The repository baseline for this major line lives at `benchmarks/baselines/v12.0.0/` and contains the raw
`*-report-full*.json` files generated on the GitHub-hosted Linux runner. Never create it from a workstation run:
the CPU, OS and runtime are part of a microbenchmark result.

In the manual workflow, use `mode=run` for an exploratory category, and `mode=compare` or `mode=record` only with
`category=all`. The gate compares a complete benchmark identity set; comparing a subset would make missing methods look
like a regression.

To create a candidate baseline after the manual **Benchmarks** workflow has run with `mode=record`, download its
`benchmark-baseline-candidate-*` artifact and commit the contained JSON reports after review. The same operation can be
performed locally only when the source results were produced by the comparable CI runner:

```powershell
dotnet run --project benchmarks/CaeriusNet.Benchmark.csproj -c Release -- `
  --record-baseline <candidate-results-dir> <empty-baseline-dir>
```

The comparison is intentionally strict:

- every v12 benchmark and parameter combination must exist on both sides;
- candidate median time may not exceed its baseline by more than 5%;
- any increase in allocated bytes per operation fails the gate;
- missing, malformed or empty baseline data fails with a clear bootstrap message.

Run the comparator manually with:

```powershell
dotnet run --project benchmarks/CaeriusNet.Benchmark.csproj -c Release -- `
  --compare benchmarks/baselines/v12.0.0 <candidate-results-dir> --max-time-regression 5
```

It writes `caerius-benchmark-comparison.md` next to the candidate results and exits non-zero on a regression. The
workflow runs this gate on pull requests and release publication. A release trigger happens after publication, so the
pull-request gate is the enforcement point before merging.

## Reproducibility limits

The gate uses a pinned GitHub runner image and fixed BenchmarkDotNet configuration, but GitHub-hosted runners do not
guarantee identical CPU silicon, turbo state, or background load. A failure is therefore a release-quality signal to
investigate using the attached full JSON report, not proof of a regression in isolation. New baselines must be captured
on the same CI runner family, and deliberate performance/allocations changes require an explicitly reviewed baseline
update.
