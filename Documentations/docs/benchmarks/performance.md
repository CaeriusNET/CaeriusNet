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
- **CI mode**: `Job.Dry` with 1 warmup + 3 iterations to prevent CI timeout
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

<!--@include: ./results/DtoMappingBench.md-->

> **Key insight:** The source-generated mapper is on-par with hand-written code. The pre-allocated array variant saves the `List<T>` internal array doubling overhead (~25% less allocation at 1K rows).

### Wide-Row DTO Mapping Scaling

**Benchmark: `WideRowDtoMappingBench`**

Measures how the generated positional constructor cost grows as DTO column count increases from 5 → 10.
Each additional column adds one typed `reader.GetXxx(ordinal)` call. Uses `List<T>` and pre-allocated array variants.

<!--@include: ./results/WideRowDtoMappingBench.md-->

> **Key insight:** Column count adds a near-linear cost per extra column per 1K rows. Pre-allocating as an array rather than `List<T>` consistently saves ~20% allocation.

### Nullable Column Mapping (IsDBNull Cost)

**Benchmark: `NullableColumnMappingBench`**

Measures the `reader.IsDBNull(i) ? null : reader.GetXxx(i)` overhead generated for nullable fields.
Tests three null densities (0%, 50%, 100%) to quantify branch predictor impact.

<!--@include: ./results/NullableColumnMappingBench.md-->

> **Key insight:** The `IsDBNull` check adds overhead vs a non-nullable DTO.
> At 100% nulls, the branch predictor learns the pattern quickly.
> At 50% mixed nulls, the misprediction rate peaks — worst case for branch predictor.

### StoredProcedureParametersBuilder

**Benchmark: `SpParameterBuilderBench`**

Measures the cost of constructing a `StoredProcedureParametersBuilder` and calling `.Build()`.

<!--@include: ./results/SpParameterBuilderBench.md-->

> **Key insight:** Initial pre-allocation avoids resizing up to 4 parameters.
> For typical SPs (3–8 parameters), the builder overhead is sub-microsecond.

### AddTvpParameter — List vs IEnumerable

**Benchmark: `AddTvpParameterBench`**

The builder contains an internal fast-path: `items is IList<T> list ? list : items.ToList()`.
This benchmark quantifies the difference between passing a pre-materialised `List<T>` vs a lazy `IEnumerable<T>`.

<!--@include: ./results/AddTvpParameterBench.md-->

> ⚠️ **Always pass `List<T>` (or `IList<T>`) to `AddTvpParameter`** — never a LINQ chain.
> The `IEnumerable<T>` path forces a `.ToList()` materialisation inside the builder, causing O(N) allocation vs O(1) for the `List<T>` fast-path.

### TVP Serialization

**Benchmark: `TvpSerializationBench`**

Measures the cost of `ITvpMapper.MapAsSqlDataRecords()` — converting `List<T>` into `IEnumerable<SqlDataRecord>`.
The source-generated implementation reuses a single `SqlDataRecord` instance per iteration (lazy streaming pattern).

<!--@include: ./results/TvpSerializationBench.md-->

> **Key insight:** The lazy streaming pattern allocates a constant number of bytes regardless of row count
> (1 `SqlDataRecord` instance + iterator state). This is the fundamental advantage of the source-generator
> TVP approach over `DataTable`-based implementations which allocate O(N) memory.

### TVP vs DataTable — Allocation Comparison

**Benchmark: `TvpVsDataTableBench`**

Direct comparison between CaeriusNet's O(1) `SqlDataRecord` streaming and the traditional `DataTable` approach.

<!--@include: ./results/TvpVsDataTableBench.md-->

> **Key insight:** At scale, CaeriusNet allocates orders of magnitude less than `DataTable`.
> The `BeginLoadData/EndLoadData` optimisation helps DataTable marginally but remains incomparable to streaming.

### TVP Column-Count Scaling

**Benchmark: `TvpColumnScalingBench`**

Measures how TVP serialization cost scales as column count grows: 3 → 5 → 10 columns.
Each additional column adds one `record.SetXxx(ordinal, value)` call per row.

<!--@include: ./results/TvpColumnScalingBench.md-->

> **Key insight:** Memory allocation stays **constant** regardless of column count.
> Time cost scales linearly with columns × rows. The `SqlMetaData[]` schema array is `static readonly` —
> allocated once per type at JIT time, never per-call.

---

## Collection Type Benchmarks

These benchmarks compare the performance of different .NET collection types
for reading and creating result sets. This helps developers choose the right
return type for their use case.

### Reading Collections (1–10K items)

**Benchmark: `ReadListToBench`**

<!--@include: ./results/ReadListToBench.md-->

**Benchmark: `ReadReadOnlyCollectionToBench`**

<!--@include: ./results/ReadReadOnlyCollectionToBench.md-->

**Benchmark: `ReadEnumerableToBench`**

<!--@include: ./results/ReadEnumerableToBench.md-->

**Benchmark: `ReadImmutableArrayToBench`**

<!--@include: ./results/ReadImmutableArrayToBench.md-->

> `ImmutableArray<T>` benefits from cache-friendly contiguous memory layout for read-heavy workloads.

### Creating Collections (with pre-allocated capacity)

**Benchmark: `CreateListToBench`**

<!--@include: ./results/CreateListToBench.md-->

**Benchmark: `CreateReadOnlyCollectionToBench`**

<!--@include: ./results/CreateReadOnlyCollectionToBench.md-->

**Benchmark: `CreateEnumerableToBench`**

<!--@include: ./results/CreateEnumerableToBench.md-->

**Benchmark: `CreateImmutableArrayToBench`**

<!--@include: ./results/CreateImmutableArrayToBench.md-->

### List Capacity Pre-allocation Impact

**Benchmark: `ListWithoutCapacityToBench` vs `ListWithCapacityToBench`**

<!--@include: ./results/ListWithoutCapacityToBench.md-->

<!--@include: ./results/ListWithCapacityToBench.md-->

<!--@include: ./results/ListWithCapacityWithOverextendToBench.md-->

<!--@include: ./results/ListWithLessCapacityThanNeededToBench.md-->

> **Always pre-allocate `List<T>` capacity** when the row count is known (use `ResultSetCapacity` in the builder).

---

## SQL Server Benchmarks

These benchmarks measure real end-to-end performance with a SQL Server 2022 instance.
They require the `BENCHMARK_SQL_CONNECTION` environment variable to be set.

> ⚠️ These are **real measured values** produced by the CI benchmark workflow. Numbers below are populated automatically after each [GitHub Release](https://github.com/CaeriusNET/CaeriusNet/releases).

### Stored Procedure Execution Roundtrip

**Benchmark: `SpExecutionBench`**

Full roundtrip: open connection → execute SP → read all rows → return count.

<!--@include: ./results/SpExecutionBench.md-->

> **Note:** Connection open is the dominant cost for small result sets.
> For production usage, always use connection pooling (ADO.NET default behaviour).

### Batched vs Single Inserts (The TVP Advantage)

**Benchmark: `BatchedVsSingleBench`**

This is the **core value proposition** of CaeriusNet's TVP support.

<!--@include: ./results/BatchedVsSingleBench.md-->

> **Key insight:** TVP batch inserts eliminate the N×roundtrip overhead.
> This gap widens dramatically with network latency in production environments.

### Multi-Result Set vs Separate Calls

**Benchmark: `MultiResultSetBench`**

<!--@include: ./results/MultiResultSetBench.md-->

> Combining multiple result sets in a single roundtrip eliminates redundant TDS overhead.

### Full TVP Lifecycle Roundtrip

**Benchmark: `TvpFullRoundtripBench`**

Measures the complete TVP pipeline end-to-end: generate items → `AddTvpParameter<T>` → `Build()` → execute SP with `OUTPUT INSERTED.*` → stream back results.
Compared against manual `SqlParameter(Structured)` setup without the builder.

<!--@include: ./results/TvpFullRoundtripBench.md-->

> **Key insight:** The CaeriusNet builder adds negligible overhead vs raw ADO.NET — negligible against the network roundtrip cost.

### OUTPUT Parameter vs SCOPE_IDENTITY() Anti-pattern

**Benchmark: `SpOutputParameterBench`**

Compares `@NewId INT OUTPUT` (1 roundtrip) vs a separate `SELECT SCOPE_IDENTITY()` (2 roundtrips).

<!--@include: ./results/SpOutputParameterBench.md-->

> **Key insight:** The two-roundtrip `SCOPE_IDENTITY()` pattern is significantly slower than using `OUTPUT` parameters.
> Always use `@NewId INT OUTPUT` (or `OUTPUT INSERTED.*` for multi-row) to retrieve identity values.

### Connection Pool Reuse vs Cold Start

**Benchmark: `ConnectionPoolBench`**

Quantifies the cost difference between warm pool, cold start, and persistent connection reuse.

<!--@include: ./results/ConnectionPoolBench.md-->

> **Key insight:** ADO.NET connection pooling drastically reduces connection overhead.
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

> All benchmark results displayed on this page are **real measured values** produced by the CI benchmark workflow.
> Tables are populated automatically after each [GitHub Release](https://github.com/CaeriusNET/CaeriusNet/releases).
> If a section shows a placeholder message, it means no benchmark run has been committed to the repository yet —
> trigger the [benchmark workflow](https://github.com/CaeriusNET/CaeriusNet/actions/workflows/benchmark.yml) to generate fresh results.
