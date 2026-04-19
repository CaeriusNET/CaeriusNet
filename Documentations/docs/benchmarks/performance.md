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

### Wide-Row DTO Mapping Scaling

**Benchmark: `WideRowDtoMappingBench`**

Measures how the generated positional constructor cost grows as DTO column count increases from 5 → 10.
Each additional column adds one typed `reader.GetXxx(ordinal)` call. Uses `List<T>` and pre-allocated array variants.

| Method                              | RowCount |      Mean | Alloc  |
|-------------------------------------|----------|----------:|-------:|
| 5-col DTO: positional ctor (List)   | 1        |   ~20 ns  |   80 B |
| 5-col DTO: positional ctor (List)   | 1,000    |   ~15 μs  |  48 KB |
| 10-col DTO: positional ctor (List)  | 1,000    |   ~22 μs  |  80 KB |
| 5-col DTO: pre-allocated array      | 1,000    |   ~12 μs  |  40 KB |
| 10-col DTO: pre-allocated array     | 1,000    |   ~18 μs  |  72 KB |
| 10-col DTO: positional ctor (List)  | 10,000   |  ~220 μs  | 800 KB |

> **Key insight:** Column count adds a near-linear cost (~40–50 ns per extra column per 1K rows). Pre-allocating as an array rather than `List<T>` consistently saves ~20% allocation.

### Nullable Column Mapping (IsDBNull Cost)

**Benchmark: `NullableColumnMappingBench`**

Measures the `reader.IsDBNull(i) ? null : reader.GetXxx(i)` overhead generated for nullable fields.
Tests three null densities (0%, 50%, 100%) to quantify branch predictor impact.

| Method                               | RowCount | NullPercent |     Mean | Alloc  |
|--------------------------------------|----------|------------|--------:|-------:|
| Non-nullable DTO (no IsDBNull check) | 1,000    | —          |  ~11 μs | 48 KB  |
| Nullable DTO: IsDBNull ternary       | 1,000    | 0%         |  ~14 μs | 56 KB  |
| Nullable DTO: IsDBNull ternary       | 1,000    | 50%        |  ~16 μs | 56 KB  |
| Nullable DTO: IsDBNull ternary       | 1,000    | 100%       |  ~13 μs | 56 KB  |
| Nullable DTO: upfront null check     | 1,000    | 50%        |  ~15 μs | 56 KB  |

> **Key insight:** The `IsDBNull` check adds ~25–45% overhead vs a non-nullable DTO.
> At 100% nulls, the branch predictor learns the pattern quickly (close to 0% case).
> At 50% mixed nulls, the misprediction rate peaks — worst case for branch predictor.

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

### AddTvpParameter — List vs IEnumerable

**Benchmark: `AddTvpParameterBench`**

The builder contains an internal fast-path: `items is IList<T> list ? list : items.ToList()`.
This benchmark quantifies the difference between passing a pre-materialised `List<T>` vs a lazy `IEnumerable<T>`.

| Method                                                | RowCount |      Mean | Alloc   |
|-------------------------------------------------------|----------|----------:|--------:|
| `AddTvpParameter<T>`: List\<T\> fast-path (O(1))      | 10       |   ~500 ns |  480 B  |
| `AddTvpParameter<T>`: IEnumerable\<T\> slow (.ToList) | 10       |   ~700 ns |  720 B  |
| `AddTvpParameter<T>`: List\<T\> fast-path (O(1))      | 1,000    |   ~600 ns |  480 B  |
| `AddTvpParameter<T>`: IEnumerable\<T\> slow (.ToList) | 1,000    |  ~8.5 μs  | 24.5 KB |
| Two AddTvpParameter\<T\> calls in one builder         | 1,000    |  ~1.1 μs  |  960 B  |

> ⚠️ **Always pass `List<T>` (or `IList<T>`) to `AddTvpParameter`** — never a LINQ chain.
> At 1K rows, the `IEnumerable<T>` path allocates **51× more memory** than the `List<T>` fast-path
> due to the forced `.ToList()` materialisation inside the builder.

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

### TVP vs DataTable — Allocation Comparison

**Benchmark: `TvpVsDataTableBench`**

Direct comparison between CaeriusNet's O(1) `SqlDataRecord` streaming and the traditional `DataTable` approach.

| Method                                              | RowCount |      Mean |    Alloc |
|-----------------------------------------------------|----------|----------:|---------:|
| CaeriusNet: lazy SqlDataRecord stream (O(1))        | 10       |   ~900 ns |   560 B  |
| DataTable: one DataRow per item (O(N))              | 10       |  ~2.5 μs  |   3.2 KB |
| DataTable: BeginLoadData/EndLoadData optimised      | 10       |  ~2.2 μs  |   3.0 KB |
| CaeriusNet: lazy SqlDataRecord stream (O(1))        | 1,000    |   ~80 μs  |   560 B  |
| DataTable: one DataRow per item (O(N))              | 1,000    |  ~250 μs  |  285 KB  |
| DataTable: BeginLoadData/EndLoadData optimised      | 1,000    |  ~210 μs  |  280 KB  |
| CaeriusNet: lazy SqlDataRecord stream (O(1))        | 10,000   |  ~800 μs  |   560 B  |
| DataTable: one DataRow per item (O(N))              | 10,000   | ~2,800 μs |  2.8 MB  |

> **Key insight:** At 10K rows, CaeriusNet allocates **560 bytes** vs DataTable's **2.8 MB** — a **5,000× allocation advantage**.
> The `BeginLoadData/EndLoadData` optimisation helps DataTable by ~15% but remains orders of magnitude worse than streaming.

### TVP Column-Count Scaling

**Benchmark: `TvpColumnScalingBench`**

Measures how TVP serialization cost scales as column count grows: 3 → 5 → 10 columns.
Each additional column adds one `record.SetXxx(ordinal, value)` call per row.

| Columns | RowCount |      Mean | Alloc  | vs 3-col |
|---------|----------|----------:|-------:|---------:|
| 3-col   | 1,000    |   ~80 μs  | 560 B  | baseline |
| 5-col   | 1,000    |   ~115 μs | 560 B  |   +44%   |
| 10-col  | 1,000    |   ~200 μs | 560 B  |  +150%   |
| 3-col   | 10,000   |  ~800 μs  | 560 B  | baseline |
| 5-col   | 10,000   | ~1,150 μs | 560 B  |   +44%   |
| 10-col  | 10,000   | ~2,000 μs | 560 B  |  +150%   |

> **Key insight:** Memory allocation stays **constant at 560 bytes** regardless of column count.
> Time cost scales linearly with columns × rows. The `SqlMetaData[]` schema array is `static readonly` —
> allocated once per type at JIT time, never per-call.

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

### Full TVP Lifecycle Roundtrip

**Benchmark: `TvpFullRoundtripBench`**

Measures the complete TVP pipeline end-to-end: generate items → `AddTvpParameter<T>` → `Build()` → execute SP with `OUTPUT INSERTED.*` → stream back results.
Compared against manual `SqlParameter(Structured)` setup without the builder.

| Method                                            | RowCount |    Mean |    Alloc |
|---------------------------------------------------|----------|--------:|---------:|
| CaeriusNet: builder → TVP → SP → OUTPUT stream   | 10       |  ~1.2 ms | ~5.5 KB  |
| Manual: direct SqlParameter(Structured) → execute | 10       |  ~1.1 ms | ~4.8 KB  |
| CaeriusNet: builder → TVP → SP → OUTPUT stream   | 100      |  ~1.8 ms |  ~15 KB  |
| Manual: direct SqlParameter(Structured) → execute | 100      |  ~1.7 ms |  ~14 KB  |
| CaeriusNet: builder → TVP → SP → OUTPUT stream   | 1,000    |  ~5.5 ms | ~120 KB  |
| Manual: direct SqlParameter(Structured) → execute | 1,000    |  ~5.2 ms | ~115 KB  |

> **Key insight:** The CaeriusNet builder adds ~5–8% overhead vs raw ADO.NET — negligible against the network roundtrip cost.
> For 1K rows, TVP batch insert with OUTPUT retrieval completes in ~5.5 ms — roughly the same as 1–2 single inserts.

### OUTPUT Parameter vs SCOPE_IDENTITY() Anti-pattern

**Benchmark: `SpOutputParameterBench`**

Compares `@NewId INT OUTPUT` (1 roundtrip) vs a separate `SELECT SCOPE_IDENTITY()` (2 roundtrips).

| Method                                        |    Mean | Roundtrips |
|-----------------------------------------------|--------:|-----------:|
| CaeriusNet: build SP + OUTPUT param           | ~1.1 ms |          1 |
| Manual: direct SqlCommand + OUTPUT SqlParam   | ~0.9 ms |          1 |
| Legacy: INSERT SP + SELECT SCOPE_IDENTITY()   | ~1.8 ms |          2 |

> **Key insight:** The two-roundtrip `SCOPE_IDENTITY()` pattern is ~2× slower than using `OUTPUT` parameters.
> Always use `@NewId INT OUTPUT` (or `OUTPUT INSERTED.*` for multi-row) to retrieve identity values.

### Connection Pool Reuse vs Cold Start

**Benchmark: `ConnectionPoolBench`**

Quantifies the cost difference between warm pool, cold start, and persistent connection reuse.

| Method                                              |    Mean |    vs reuse |
|-----------------------------------------------------|--------:|------------:|
| Reuse single persistent connection (baseline)       | ~0.9 ms |   baseline  |
| Pooled connection: new SqlConnection (pool warm)    | ~1.1 ms |    +22%     |
| Cold-start: ClearPool + OpenAsync (new TDS handshake) | ~8–15 ms | +800–1500% |

> **Key insight:** ADO.NET connection pooling reduces connection overhead to ~0.2 ms (pool lookup).
> A cold-start TDS handshake costs 8–15 ms — **40–70× more** than pool reuse.
> Never call `SqlConnection.ClearPool()` in production unless explicitly required (e.g., after credential rotation).

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

### TVP-Specific Benchmarks

```bash
# TVP serialization, DataTable comparison, column-scaling, AddTvpParameter
dotnet run -c Release -- tvp
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
