---
title: In-memory benchmarks
description: CaeriusNet v12 CPU and allocation benchmarks for command construction, mapping, scalars, materialization and TVP writing.
---

# In-memory benchmarks

The v12 in-memory suite measures code paths without network or SQL Server latency. Its purpose is to catch allocation and throughput regressions in the runtime's hot paths; it is not a replacement for the end-to-end SQL Server acceptance matrix.

## Command construction

**Benchmark class:** `CommandBuilderBenchmarks`

`BuildScalarCommand` measures construction of an immutable `StoredProcedureCommand` with input, input/output, output and return-value descriptors. `BuildTvpCommand` adds a structured descriptor, and `BuildCanonicalCacheKey` measures the centralized canonical key calculation.

The builder stores immutable descriptors and validates names, directions, ordinals and SQL facets before opening a connection. It does not allocate `SqlParameter` objects while a command is being described; those objects are created only for execution.

## Mapping, scalar conversion and collections

**Benchmark class:** `MaterializationBenchmarks`

The suite measures representative DTO construction and the conversion work behind typed scalar results. It also compares the three result families for procedures returning **1, 2, 5 and 10** result sets:

| Benchmark | v12 shape |
| --- | --- |
| `MaterializeEnumerableResultSets` | materialized `IEnumerable<T>` |
| `MaterializeReadOnlyCollectionResultSets` | `ReadOnlyCollection<T>` |
| `MaterializeImmutableArrayResultSets` | `ImmutableArray<T>` |

`IEnumerable<T>` is deliberately materialized in v12. `StreamAsync<T>` is the separate API for a reader-backed single result stream and is not comparable to these collection allocations.

## Table-valued parameters

**Benchmark class:** `TvpBenchmarks`

Rows are tested at 1, 1 000 and 100 000 elements. `WriteRowsIntoOneReusableSqlDataRecord` exercises the static `ITvpMapper<T>.WriteRow` implementation against one reusable `SqlDataRecord`, exactly as execution does. `BuildStructuredParameterDescriptor` measures only command description; it does not enumerate rows.

This validates the v12 TVP design: static SQL type/metadata, no first-item inference, no metadata clone per invocation, and support for empty or lazy inputs at execution time.

## Reading the results

Use `Mean`, `Ratio` and `Allocated` together. A regression is relevant when it is repeatable under comparable hardware and either exceeds the release threshold or adds allocation to a hot path. The release process records an explanation or correction for a regression above 5 percent.

Run the suite with:

```bash
dotnet run -c Release --project benchmarks/CaeriusNet.Benchmark.csproj -- --filter "*Benchmarks*"
```
