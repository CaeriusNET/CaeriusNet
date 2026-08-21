---
title: Collection benchmarks
description: CaeriusNet v12 materialized result-family benchmarks.
---

# Collection benchmarks

CaeriusNet v12 retains three materialized result shapes. The same `MaterializationBenchmarks` suite uses 1, 2, 5 and 10 result sets so it captures allocation that grows with the number of independently materialized sets.

| Runtime API | Returned tuple element | Intended use |
| --- | --- | --- |
| `QueryMultipleAsync<T1, …, Tn>` | `IEnumerable<T>` | Consumption ergonomics; it is already materialized. |
| `QueryMultipleReadOnlyCollectionAsync<T1, …, Tn>` | `ReadOnlyCollection<T>` | Read-only indexed collection semantics. |
| `QueryMultipleImmutableArrayAsync<T1, …, Tn>` | `ImmutableArray<T>` | Immutable value-style collection, including cache-friendly snapshots. |

Each multi-result overload accepts a capacity for each set. Capacity is an allocation hint, not an assertion about SQL row count. The set-count contract is separate: by default the procedure must return exactly the announced number of sets.

For a single reader-backed result set, benchmark `StreamAsync<T>` in an application-level scenario. It owns a connection and reader for the life of enumeration and bypasses cache, so treating it as another materialized collection would be misleading.

Run the collection cases with:

```bash
dotnet run -c Release --project benchmarks/CaeriusNet.Benchmark.csproj -- --filter "*MaterializationBenchmarks*"
```
