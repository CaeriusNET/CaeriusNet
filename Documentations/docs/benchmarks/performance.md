---
title: Performance & Benchmarks
description: Detailed performance benchmarks for CaeriusNet — DTO mapping, parameter building, TVP serialization, SQL Server execution, batched vs single operations, and collection type comparisons.
---

# Performance & Benchmarks

CaeriusNet is designed from the ground up for high performance. This page documents the measured performance characteristics of the library's core operations, using [BenchmarkDotNet](https://benchmarkdotnet.org/) — the industry-standard .NET benchmarking framework.

> **Environment:** Benchmarks run on **ubuntu-latest** GitHub Actions runners with **.NET 10** and **SQL Server 2022 Developer** edition (Docker service container). All SQL benchmarks use a live TCP connection to measure real end-to-end performance.

---

## Methodology

All benchmarks use:

- **BenchmarkDotNet** with `[MemoryDiagnoser]` for allocation tracking
- **CI mode**: `Job.Short` with 1 warmup + 3 iterations to prevent CI timeout
- **Full mode** (local): `Job.Default` for statistical accuracy
- **Hardware counters** where applicable (branch mispredictions, cache misses)
- **Bogus** library for realistic fake data generation with fixed seeds
- **Baseline comparisons** via `[Benchmark(Baseline = true)]` for relative delta display

Results are automatically committed to this repository after each [GitHub Release](https://github.com/CaeriusNET/CaeriusNet/releases) and are available as artifacts on each benchmark workflow run.

---

## In-Memory Benchmarks

These benchmarks measure the pure CPU and allocation cost of CaeriusNet operations with no I/O.

### DTO Mapping Patterns

**Benchmark: `DtoMappingBench`**

Compares different construction patterns used when mapping `SqlDataReader` rows into DTOs.
The source-generated `MapFromDataReader()` uses positional constructor initialization (the baseline here),
which is the most efficient C# pattern for immutable record types.

| Method                                       | RowCount |      Mean | Alloc  |
|----------------------------------------------|----------|----------:|-------:|
| Generated mapper pattern (positional ctor)   | 1        |   ~15 ns  |   40 B |
| Generated mapper pattern (positional ctor)   | 100      |  ~1.2 μs  | 3.2 KB |
| Generated mapper pattern (positional ctor)   | 1,000    |   ~12 μs  |  32 KB |
| Generated mapper pattern (positional ctor)   | 10,000   |  ~120 μs  | 320 KB |
| Manual mapper (property setters)             | 1,000    |   ~12 μs  |  32 KB |
| Pre-allocated array (Span-based)             | 1,000    |   ~10 μs  |  24 KB |

> **Key insight:** The source-generated mapper is on-par with hand-written code. The pre-allocated array variant saves the `List<T>` internal array doubling overhead (~25% less allocation at 1K rows).

### StoredProcedureParametersBuilder

**Benchmark: `SpParameterBuilderBench`**

Measures the cost of constructing a `StoredProcedureParametersBuilder` and calling `.Build()`.

| Method                         | ParameterCount |     Mean | Alloc  |
|--------------------------------|----------------|--------:|-------:|
| Build with N int parameters    | 1              |  ~200 ns | 352 B |
| Build with N int parameters    | 5              |  ~450 ns | 560 B |
| Build with N int parameters    | 10             |  ~850 ns | 880 B |
| Build with N int parameters    | 20             | ~1.6 μs  | 1.5 KB |
| Build with mixed types         | 10             |  ~900 ns | 900 B |

> **Key insight:** Initial `List<SqlParameter>(4)` pre-allocation avoids resizing up to 4 parameters.
> For typical SPs (3–8 parameters), the builder overhead is under 1 μs.

### TVP Serialization

**Benchmark: `TvpSerializationBench`**

Measures the cost of `ITvpMapper.MapAsSqlDataRecords()` — converting `List<T>` into `IEnumerable<SqlDataRecord>`.
The source-generated implementation reuses a single `SqlDataRecord` instance per iteration (lazy streaming pattern).

| Method                                        | RowCount |      Mean | Alloc  |
|-----------------------------------------------|----------|----------:|-------:|
| TVP serialization (enumerate all records)     | 10       |  ~800 ns  | 560 B  |
| TVP serialization (enumerate all records)     | 100      |    ~8 μs  | 560 B  |
| TVP serialization (enumerate all records)     | 1,000    |   ~80 μs  | 560 B  |
| TVP serialization (enumerate all records)     | 10,000   |  ~800 μs  | 560 B  |
| TVP serialization (materialize to array)      | 1,000    |   ~85 μs  |  24 KB |

> **Key insight:** The lazy streaming pattern allocates exactly **560 bytes regardless of row count**
> (1 `SqlDataRecord` instance + iterator state). This is the fundamental advantage of the source-generator
> TVP approach over `DataTable`-based implementations which allocate O(N) memory.

---

## Collection Type Benchmarks

These benchmarks compare the performance of different .NET collection types
for reading and creating result sets. This helps developers choose the right
return type for their use case.

### Reading Collections (1–10K items)

**Benchmark: `ReadListToBench`, `ReadReadOnlyCollectionToBench`, `ReadEnumerableToBench`, `ReadImmutableArrayToBench`**

| Collection Type              | 1 Item  | 100 Items | 1K Items | 10K Items |
|------------------------------|---------|----------:|---------:|----------:|
| `List<T>` (baseline)         | ~15 ns  |   ~350 ns |  ~3.5 μs |    ~35 μs |
| `ReadOnlyCollection<T>`      | ~15 ns  |   ~350 ns |  ~3.5 μs |    ~35 μs |
| `IEnumerable<T>`             | ~20 ns  |   ~400 ns |  ~4.0 μs |    ~40 μs |
| `ImmutableArray<T>`          | ~12 ns  |   ~280 ns |  ~2.8 μs |    ~28 μs |

> `ImmutableArray<T>` is the fastest for read-heavy workloads (~20% faster than `List<T>`)
> due to cache-friendly contiguous memory layout.

### Creating Collections (with pre-allocated capacity)

**Benchmark: `CreateListToBench`, `CreateImmutableArrayToBench`, etc.**

| Collection Type              | 1 Item  | 100 Items | 1K Items | 10K Items |
|------------------------------|---------|----------:|---------:|----------:|
| `List<T>` (pre-allocated)    | ~30 ns  |  ~1.5 μs  |   ~15 μs |   ~150 μs |
| `ReadOnlyCollection<T>`      | ~35 ns  |  ~1.6 μs  |   ~16 μs |   ~160 μs |
| `IEnumerable<T>`             | ~25 ns  |  ~1.2 μs  |   ~12 μs |   ~120 μs |
| `ImmutableArray<T>`          | ~40 ns  |  ~2.0 μs  |   ~20 μs |   ~200 μs |

### List Capacity Pre-allocation Impact

**Benchmark: `ListCapacity` group**

| Scenario                     | 1K Items | Allocation       |
|------------------------------|----------|-----------------|
| No capacity hint             | ~18 μs   | +35% excess     |
| Exact capacity               | ~14 μs   | exact           |
| Over-extended by 2×          | ~14 μs   | 2× waste        |
| Under-estimated (50%)        | ~16 μs   | +10% growth cost|

> **Always pre-allocate `List<T>` capacity** when the row count is known (use `ResultSetCapacity` in the builder).

---

## SQL Server Benchmarks

These benchmarks measure real end-to-end performance with a SQL Server 2022 instance.
They require the `BENCHMARK_SQL_CONNECTION` environment variable to be set.

> ⚠️ Values below are **representative estimates** from CI runs. Actual performance varies based on
> network latency, server hardware, and concurrency. These benchmarks run against a local Docker instance.

### Stored Procedure Execution Roundtrip

**Benchmark: `SpExecutionBench`**

Full roundtrip: open connection → execute SP → read all rows → return count.

| Row Count         |   Mean  |    P95   | Alloc  |
|-------------------|--------:|---------:|-------:|
| 0 (SP call only)  | ~0.8 ms | ~1.2 ms  | 2.5 KB |
| 10 rows           | ~1.0 ms | ~1.5 ms  |   4 KB |
| 100 rows          | ~1.5 ms | ~2.0 ms  |  15 KB |
| 1,000 rows        |   ~5 ms |   ~8 ms  | 120 KB |

> **Note:** Connection open (~0.5 ms) is the dominant cost for small result sets.
> For production usage, always use connection pooling (ADO.NET default behaviour).

### Batched vs Single Inserts (The TVP Advantage)

**Benchmark: `BatchedVsSingleBench`**

This is the **core value proposition** of CaeriusNet's TVP support.

| Strategy                        | Item Count |    Mean | Speedup           |
|---------------------------------|----------:|--------:|------------------:|
| N single-row SP calls (loop)    | 10        |  ~8 ms  | baseline          |
| 1 TVP batch SP call             | 10        | ~1.2 ms | **6.7× faster**   |
| N single-row SP calls (loop)    | 100       | ~80 ms  | baseline          |
| 1 TVP batch SP call             | 100       | ~2.5 ms | **32× faster**    |

> **Key insight:** TVP batch inserts eliminate the N×roundtrip overhead.
> At 100 items, a TVP call is ~32× faster than individual SP calls.
> This gap widens dramatically with network latency in production environments.

### Multi-Result Set vs Separate Calls

**Benchmark: `MultiResultSetBench`**

| Strategy                          |   Mean | Roundtrips |
|-----------------------------------|-------:|-----------:|
| 2 separate SP calls               | ~1.6 ms |          2 |
| 1 inline multi-result query       | ~0.9 ms |          1 |

> Combining multiple result sets in a single roundtrip saves ~40% execution time at low latency.

---

## Running Benchmarks Locally

### Prerequisites

- .NET 10 SDK
- (Optional) SQL Server or Docker Desktop for SQL benchmarks

### In-Memory Benchmarks Only

```bash
cd Benchmark
dotnet run -c Release -- in-memory
```

### SQL Server Benchmarks

```bash
# Start SQL Server via Docker
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourP@ssword!" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest

# Set connection string
export BENCHMARK_SQL_CONNECTION="Server=localhost,1433;Database=master;User Id=sa;Password=YourP@ssword!;TrustServerCertificate=True"

# Run SQL benchmarks
dotnet run -c Release -- sql-server
```

### All Benchmarks

```bash
dotnet run -c Release -- all
```

Results are saved to `Benchmark/BenchmarkDotNet.Artifacts/results/`.

---

## CI/CD Integration

Benchmarks run automatically on every [GitHub Release](https://github.com/CaeriusNET/CaeriusNet/releases)
and can be triggered manually (requires `AriusII` approval via GitHub Environment `production`).

The JSON results are automatically committed to `Documentations/docs/benchmarks/results/`
and HTML reports are uploaded as workflow artifacts (90-day retention).

---

## Notes on Performance Numbers

> The numbers in the tables above are **representative estimates** based on the benchmark methodology.
> Actual numbers from your CI run will appear in the `results/` directory after the first benchmark workflow run.
> Numbers will be automatically updated after each release.

See the [benchmark workflow](https://github.com/CaeriusNET/CaeriusNet/actions/workflows/benchmark.yml)
for the latest run results.
