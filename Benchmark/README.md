# CaeriusNet Benchmarks

## Overview

BenchmarkDotNet benchmarks measure mapping throughput, collection behavior, cache performance, TVP serialization, and
end-to-end SQL Server execution.

## Prerequisites

- .NET 10 SDK
- For SQL Server benchmarks: SQL Server 2022 or Docker Desktop

## Running Benchmarks

### All categories

```bash
cd Benchmark
dotnet run -c Release
```

### By category

```bash
dotnet run -c Release -- in-memory
dotnet run -c Release -- tvp
dotnet run -c Release -- collections
dotnet run -c Release -- cache
dotnet run -c Release -- sql-server
```

### SQL Server benchmarks

```bash
set BENCHMARK_SQL_CONNECTION=Server=localhost,1433;Database=master;User Id=sa;Password=YourP@ssword!;TrustServerCertificate=True
dotnet run -c Release -- sql-server
```

If `BENCHMARK_SQL_CONNECTION` is not set, SQL Server benchmarks are skipped.

## Benchmark Categories

| Category          | Scope                                                                                 | I/O        |
|-------------------|---------------------------------------------------------------------------------------|------------|
| Cache             | `FrozenDictionary` and `IMemoryCache` throughput                                      | None       |
| CreateCollections | Collection creation performance                                                       | None       |
| ListCapacity      | Pre-allocation versus dynamic growth                                                  | None       |
| Mapping           | DTO `MapFromDataReader` throughput                                                    | None       |
| Parameters        | `StoredProcedureParametersBuilder` construction                                       | None       |
| ReadCollections   | Collection read access patterns                                                       | None       |
| SqlServer         | Stored procedure execution, batching, multi-result sets, TVP round-trips, and pooling | SQL Server |
| Tvp               | `SqlDataRecord` serialization and DataTable comparison                                | None       |

## Project Structure

| Path                           | Purpose                                             |
|--------------------------------|-----------------------------------------------------|
| `Program.cs`                   | Entry point and category routing                    |
| `RunningBenchmarks.cs`         | Benchmark suite orchestration                       |
| `Workshops/BenchmarkConfig.cs` | BenchmarkDotNet configuration for CI and local runs |
| `Workshops/Benchs/`            | Benchmark classes organized by category             |
| `Data/Simple/`                 | Handwritten DTO and TVP models                      |
| `Data/Generated/`              | Source-generated benchmark models                   |

## Configuration Modes

### CI mode

Used by the benchmark workflow when `BENCHMARK_ARTIFACTS_PATH` is set.

- `CI=true` enables the CI job configuration
- `BENCHMARK_ARTIFACTS_PATH` sets the output directory
- `InProcessEmitToolchain` avoids child-process artifact path issues
- 1 warmup iteration and 5 measurement iterations
- GitHub Markdown and JSON export

### Local mode

Default outside CI.

- Standard BenchmarkDotNet out-of-process execution
- Automatic warmup and iteration selection
- HTML, GitHub Markdown, and JSON export

## Results

Results are written to `BenchmarkDotNet.Artifacts/results/` by default.

In CI, `BENCHMARK_ARTIFACTS_PATH` redirects artifacts to the workflow output directory. The benchmark workflow then
copies results into `Documentations/docs/benchmarks/results/`, where they are published on the documentation site.

## Reproducibility

Benchmark data generation uses fixed seeds such as `Random(42)` and `Bogus.Randomizer.Seed` so runs stay comparable
across environments.
