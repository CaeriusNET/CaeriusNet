---
title: Performance & Benchmarks — Overview
description: CaeriusNet benchmark methodology, BDN configuration, CI vs local modes, and how to interpret BenchmarkDotNet result tables.
---

# Performance & Benchmarks

This section documents CaeriusNet benchmark suites by concern, using [BenchmarkDotNet](https://benchmarkdotnet.org/) to measure CPU, allocation, cache, and SQL Server roundtrip behavior.

> **Benchmark environment:** All CI runs execute on **ubuntu-latest** GitHub Actions runners with **.NET 10**
> and **SQL Server 2022 Developer** edition (Docker service container).
> SQL benchmarks use a live TCP connection to measure real end-to-end latency including connection pooling, TDS framing, and SQL Server execution plans.

::: warning Environment-specific results
Benchmark results are diagnostic data, not performance guarantees. Compare trends and allocations, then run the suites against your own hardware, SQL Server edition, schema, query plans, indexes, network path, and payload sizes.
:::

---

## Sections

| Page | Scope |
|---|---|
| [In-Memory Benchmarks](./in-memory) | DTO mapping, TVP serialization, parameter builder — pure CPU/allocation, no I/O |
| [Collection Benchmarks](./collections) | Read / create performance of `List<T>`, `ReadOnlyCollection<T>`, `IEnumerable<T>`, `ImmutableArray<T>` |
| [SQL Server Benchmarks](./sql-server) | End-to-end stored procedure execution, batched vs single inserts, TVP full roundtrip |
| [Cache Benchmarks](./cache) | `FrozenDictionary<K,V>` (frozen cache) and `IMemoryCache` (in-memory cache) throughput |

---

## Benchmark Methodology

### BenchmarkDotNet Configuration

CaeriusNet uses a custom `BenchmarkConfig` class (see `Benchmark/Workshops/BenchmarkConfig.cs`) that selects between two modes at runtime:

**CI Mode** (activated when the `BENCHMARK_ARTIFACTS_PATH` environment variable is set):

| Setting | Value | Rationale |
|---|---|---|
| Toolchain | `InProcessEmitToolchain` | No child-process overhead; benchmarks run in the same process as the host |
| WarmupCount | `1` | Minimal JIT warm-up — sufficient for in-process execution |
| IterationCount | `5` | Enough statistical signal for median/mean without exceeding CI time budgets |
| Exporters | `MarkdownExporter.GitHub`, `JsonExporter.Full` | Produces human-readable Markdown tables and machine-readable JSON artifacts |

**Local Mode** (default when running `dotnet run -c Release`):

| Setting | Value | Rationale |
|---|---|---|
| Toolchain | `Job.Default` | Full out-of-process benchmark with BDN's default statistical methodology |
| WarmupCount | Auto | BDN's pilot/warmup heuristic for reliable steady-state measurement |
| IterationCount | Auto | BDN targets a confidence interval < 2% relative error |
| Exporters | `HtmlExporter`, `MarkdownExporter.GitHub`, `JsonExporter.Full` | Full reports locally, including HTML timeline charts |

### Fixed Seed Reproducibility

All benchmarks that generate data (collection, mapping, TVP) use either:
- **`Randomizer.Seed = new Random(42)`** (Bogus-based SQL/mapping benchmarks) — ensures the same sequence of fake records on every run
- **`new Random(42)` in `[GlobalSetup]`** (collection benchmarks) — for speed at 100 000-item param sizes where Bogus would add measurable setup cost

This means two runs on the same hardware use identical generated inputs. Remaining differences can still come from OS scheduling, CPU frequency changes, background services, container state, SQL Server plan choices, and storage or network variability.

### Benchmark Class Architecture

Every benchmark class follows this structure:

```csharp
[Config(typeof(BenchmarkConfig))]   // picks CI vs local mode
[MemoryDiagnoser]                   // reports Gen0/1/2 GC collections + Allocated bytes
public class MyBench
{
    [Params(1, 100, 1_000, 10_000, 100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup() { /* seed-fixed data generation */ }

    [Benchmark(Baseline = true)]
    public ResultType MyBaselineMethod() { ... }

    [Benchmark]
    public ResultType MyComparisonMethod() { ... }
}
```

The `[Params]` attribute drives a matrix run: BDN generates one independent measurement per (class × param combination), making it straightforward to observe scaling behaviour across orders of magnitude.

---

## How to read a BenchmarkDotNet result table

A typical exported GitHub-Markdown table looks like:

```
| Method              | RowCount | Mean      | Error     | StdDev    | Ratio | Gen0    | Allocated |
|---------------------|----------|-----------|-----------|-----------|-------|---------|-----------|
| MapWithConstructor  | 1000     | 12.34 μs  | 0.12 μs   | 0.10 μs   | 1.00  | 1.2300  | 24.4 KB   |
| MapWithPreAlloc     | 1000     | 9.87 μs   | 0.08 μs   | 0.07 μs   | 0.80  | 0.9100  | 19.1 KB   |
```

| Column | Meaning |
|---|---|
| **Method** | Benchmark method name |
| **RowCount** | `[Params]` value driving the matrix dimension |
| **Mean** | Arithmetic mean of all measured iterations (ns / μs / ms) |
| **Error** | Half the 99.9% confidence interval — smaller = more stable |
| **StdDev** | Standard deviation across iterations — indicates noise level |
| **Ratio** | Mean divided by the baseline method mean (1.00 = baseline, 0.80 = 20% faster) |
| **Gen0 / Gen1 / Gen2** | GC collections per 1 000 invocations at each generation |
| **Allocated** | Total managed heap allocation per single invocation |

> **What to focus on:** For throughput comparisons, look at **Ratio** and **Allocated**.
> A method with Ratio < 1.00 measured faster than the baseline in that run; lower **Allocated** usually means less GC pressure.
> **Error** and **StdDev** indicate measurement confidence — high values suggest the benchmark needs more iterations or the operation is I/O-bound.

### Hardware Counters (Read / Collection Benchmarks)

Some benchmarks also include `[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.CacheMisses)]`.
These columns appear only when BDN has access to PMU (Performance Monitoring Unit) counters — available on Linux with perf support:

| Column | Meaning |
|---|---|
| **BranchMispredictions** | Number of times the CPU branch predictor was wrong per invocation. High values indicate unpredictable data-dependent branches (e.g., sparse `IsDBNull` patterns). |
| **CacheMisses** | Number of L1/L2/L3 cache miss events per invocation. High values indicate non-contiguous memory access (e.g., `IEnumerable<T>` over a heap-allocated iterator vs `ImmutableArray<T>` over a contiguous buffer). |

---

## Running Benchmarks Locally

### Prerequisites

- .NET 10 SDK
- (For SQL benchmarks) SQL Server 2022 or Docker Desktop

### By category

```bash
cd Benchmark

# In-memory only (mapping, TVP serialization, parameter builder)
dotnet run -c Release -- in-memory

# TVP-specific benchmarks
dotnet run -c Release -- tvp

# Collection read/create/capacity benchmarks
dotnet run -c Release -- collections

# Cache layer benchmarks (FrozenDictionary + IMemoryCache)
dotnet run -c Release -- cache

# SQL Server end-to-end (requires BENCHMARK_SQL_CONNECTION)
export BENCHMARK_SQL_CONNECTION="Server=localhost,1433;Database=master;User Id=sa;Password=YourP@ssword!;TrustServerCertificate=True"
dotnet run -c Release -- sql-server

# All categories
dotnet run -c Release
```

Results are written to `Benchmark/BenchmarkDotNet.Artifacts/results/` (or to `BENCHMARK_ARTIFACTS_PATH` in CI).

---

## CI/CD Integration

Benchmarks run from the repository's `benchmark.yml` GitHub Actions workflow for release-oriented runs and can be triggered manually by maintainers with the required environment approval.

After each run, the workflow extracts GitHub-Markdown tables from BDN's `*-report-github.md` artefacts. If result files are committed under `Documentations/docs/benchmarks/results/`, VitePress can publish them alongside these methodology pages.

The JSON artefacts (`*-report-full.json`) are uploaded as workflow artefacts with 90-day retention for deeper analysis.

> If a section on the benchmark pages shows a placeholder message, it means no run has been committed yet —
> trigger the [benchmark workflow](https://github.com/CaeriusNET/CaeriusNet/actions/workflows/benchmark.yml)
> to generate fresh results.
