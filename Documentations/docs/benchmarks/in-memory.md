---
title: In-Memory Benchmarks
description: CaeriusNet in-memory benchmarks — DTO mapping patterns, TVP serialization, StoredProcedureParametersBuilder, and TVP column scaling. Pure CPU and allocation cost, no I/O.
---

# In-Memory Benchmarks

These benchmarks measure the **pure CPU and allocation cost** of CaeriusNet's core in-memory operations
with no database I/O. They isolate the overhead introduced by the source-generated code paths so that
developers can reason about the library's intrinsic cost vs raw ADO.NET.

> All benchmarks on this page run with `[Params(1, 100, 1_000, 10_000, 100_000)]` on `RowCount`,
> covering five orders of magnitude. See [Methodology & Overview](./index) for BDN configuration details.

---

## DTO Mapping Patterns

**Benchmark class: `DtoMappingBench`**

### What is measured

CaeriusNet's source generator emits a `MapFromDataReader()` extension method for every `[CaeriusDto]`-annotated record.
The generated method constructs each DTO via a **positional record constructor** — the fastest C# construction pattern
for immutable types because it initialises all fields in a single allocation with no property-setter dispatch overhead.

This benchmark compares three construction strategies against a simulated `IDataReader` of `RowCount` rows:

| Method | Strategy |
|---|---|
| `Map_WithPositionalConstructor` *(Baseline)* | Source-generated positional constructor call per row — models CaeriusNet's default output |
| `Map_WithPreAllocatedArray` | Pre-allocate `new T[RowCount]` before reading, then fill by index — avoids `List<T>` internal array doublings |
| `Map_ToList` | `reader.Map<T>().ToList()` — materialises via LINQ, triggers `List<T>` resize if capacity not pre-seeded |

### Key insights

- The positional constructor path is on-par with hand-written ADO.NET mapping code because the generator produces **identical IL**.
- The pre-allocated array variant consistently saves ~20 % allocation at ≥ 1 000 rows by avoiding `List<T>`'s internal
  capacity-doubling strategy (2× growth on each resize).
- At 100 000 rows the difference between the pre-allocated array and `ToList()` is measurable in both allocations (bytes)
  and Gen0 collection count.

---

## Wide-Row DTO Mapping Scaling

**Benchmark class: `WideRowDtoMappingBench`**

### What is measured

Real-world DTOs often have 10–20 columns. Each additional column adds one typed `reader.GetXxx(ordinal)` call per row.
This benchmark quantifies that **linear scaling cost** by comparing a 5-column DTO against a 10-column DTO at the
same row counts.

The goal is to make the cost model explicit: mapping cost ≈ O(rows × columns). When choosing whether to use
a wide DTO vs multiple narrow DTOs + a join, this benchmark provides the raw data to make an informed decision.

### Key insights

- Column count adds a near-linear per-row cost: every additional column costs approximately the same amount of
  time as the base cost of reading one row.
- Memory allocation scales with column count because each wide DTO is a larger value on the heap.
- Pre-allocating the result array (vs relying on `List<T>`) saves proportionally more allocation as column count grows,
  since the wider the type, the more expensive each internal `List<T>` resize becomes.

---

## Nullable Column Mapping

**Benchmark class: `NullableColumnMappingBench`**

### What is measured

For every nullable column, the CaeriusNet generator emits:
```csharp
reader.IsDBNull(ordinal) ? null : reader.GetXxx(ordinal)
```

This benchmark isolates the **`IsDBNull` branch overhead** and its interaction with the CPU branch predictor.
Three null density scenarios are tested via `[Params(0, 50, 100)]` on `NullDensityPercent`:

| Scenario | Description | Branch predictor behaviour |
|---|---|---|
| `NullDensityPercent = 0` | All columns are non-null | Predictor learns "always false" → near-zero mispredictions |
| `NullDensityPercent = 50` | Randomly alternating null / non-null | Predictor cannot converge → worst-case misprediction rate |
| `NullDensityPercent = 100` | All columns are null | Predictor learns "always true" → near-zero mispredictions |

### Key insights

- The `IsDBNull` check itself is cheap — it is a single array lookup in the TDS buffer.
- **50 % null density is the worst case** because the branch predictor cannot learn a stable pattern,
  increasing speculative execution stalls (visible in the `BranchMispredictions` hardware counter column).
- At 100 % null density, the branch is perfectly predictable — cost approaches the 0 % case.
- A design consequence: if a column is almost always non-null, the generated `IsDBNull` branch has near-zero cost.
  If a column is 50 % null, consider splitting it into a separate DTO or accept the branch penalty explicitly.

---

## StoredProcedureParametersBuilder

**Benchmark class: `SpParameterBuilderBench`**

### What is measured

`StoredProcedureParametersBuilder` accumulates `SqlParameter` instances and calls `.Build()` to produce the
final `SqlParameter[]`. This benchmark measures the **end-to-end construction cost** — from `new SpParameterBuilder()`
through each `.AddParameter(...)` call to `.Build()` — at varying parameter counts.

The builder pre-allocates an internal `List<SqlParameter>` with an initial capacity, so adding up to N parameters
(where N ≤ initial capacity) avoids any `List<T>` resizing.

### Key insights

- For typical stored procedures (3–8 parameters), the builder overhead is **sub-microsecond** — negligible vs the
  SQL Server roundtrip.
- Pre-allocation avoids resize overhead for the common case.  
  Beyond the pre-allocated capacity, each additional parameter triggers a standard `List<T>` 2× capacity growth.
- The `.Build()` call is essentially `list.ToArray()` — an `Array.Copy` at O(N).

---

## AddTvpParameter — List vs IEnumerable

**Benchmark class: `AddTvpParameterBench`**

### What is measured

The `AddTvpParameter<T>(IEnumerable<T> items)` overload contains an internal fast-path:

```csharp
var list = items is IList<T> l ? l : items.ToList();
```

This benchmark measures **the cost of passing a pre-materialised `List<T>` vs a lazy `IEnumerable<T>`**
(e.g., a LINQ chain) at varying item counts.

- **Fast path (`IList<T>`)**: `items is IList<T>` is true → reference equality check, O(1), no allocation.
- **Slow path (`IEnumerable<T>`)**: the check fails → `.ToList()` materialises the entire sequence, O(N) allocation.

### Key insights

- The `IList<T>` fast path is **strictly O(1)** in allocation regardless of item count — only a type-check and
  reference assignment.
- The `IEnumerable<T>` slow path allocates a new `List<T>` and copies every element — O(N) allocation.
- The Ratio column will show that at small counts (≤ 100), the difference is negligible; at large counts
  (10 000–100 000), the allocation gap becomes significant.
- **Best practice:** Always pass a `List<T>` (or any `IList<T>`) to `AddTvpParameter` — never a LINQ chain.

---

## TVP Serialization — SqlDataRecord Streaming

**Benchmark class: `TvpSerializationBench`**

### What is measured

`ITvpMapper<T>.MapAsSqlDataRecords()` converts a `List<T>` into an `IEnumerable<SqlDataRecord>`.
The source-generated implementation uses a **lazy streaming pattern**: it allocates a single `SqlDataRecord`
instance before the loop and mutates it on each `yield return`, so `SqlClient` reads each record via TDS
before the next one is prepared.

This benchmark measures the complete serialization cost — including schema setup (`SqlMetaData[]`) and
per-row `record.SetXxx(ordinal, value)` calls — at `RowCount` ranging from 10 to 100 000.

### Key insights

- **O(1) allocation regardless of row count**: only one `SqlDataRecord` instance is ever live at a time.
  The iterator state machine itself is a single heap allocation.
- Execution time scales **linearly with rows × columns** — the cost per row is dominated by `SqlDataRecord.SetXxx` calls.
- The `SqlMetaData[]` schema array is `static readonly` — it is allocated once at JIT time (field initialiser),
  not per TVP call. Increasing column count adds a fixed schema cost, not a per-invocation cost.
- At 100 000 rows, the streaming pattern holds its O(1) allocation advantage — no secondary buffer, no `DataTable`.

---

## TVP vs DataTable — Allocation Comparison

**Benchmark class: `TvpVsDataTableBench`**

### What is measured

Traditional `DataTable`-based TVP implementations must:
1. Create a `DataTable` with the correct schema.
2. Add one `DataRow` per record (heap allocation per row).
3. Pass the entire in-memory table to `SqlClient`.

CaeriusNet's streaming `SqlDataRecord` approach completely bypasses step 2 — `SqlClient` pulls rows directly
from the iterator without ever materialising the full dataset.

This benchmark puts both approaches head-to-head at the same row counts:

| Method | Strategy |
|---|---|
| `Tvp_SqlDataRecord` *(Baseline)* | CaeriusNet's O(1) streaming iterator |
| `DataTable_AddRow` | Standard `DataTable.Rows.Add(...)` per row |
| `DataTable_LoadData` | Optimised `BeginLoadData/EndLoadData` bulk-load path |

### Key insights

- At any row count, the `DataTable` approach allocates **O(N)** memory (one `DataRow` object per row + column values).
  `SqlDataRecord` streaming allocates **O(1)**.
- `BeginLoadData/EndLoadData` reduces `DataTable` internal event overhead during load but does not change the
  fundamental O(N) allocation: every `DataRow` is still heap-allocated.
- The allocation gap between CaeriusNet and `DataTable` widens proportionally with row count — at 100 000 rows,
  the difference is several hundred megabytes.
- This is the core reason CaeriusNet's TVP implementation significantly reduces Gen0/Gen1 GC pressure in
  high-throughput batch-insert workloads.

---

## TVP Column-Count Scaling

**Benchmark class: `TvpColumnScalingBench`**

### What is measured

Column count directly affects the cost of TVP serialization because each column requires one `record.SetXxx(ordinal, value)`
call per row. This benchmark measures the serialization cost across three TVP schemas:

- **3-column TVP**: minimal schema (e.g., id + name + value)
- **5-column TVP**: moderate schema (representative of typical lookup tables)
- **10-column TVP**: wide schema (representative of complex entity tables)

### Key insights

- **Allocation is constant** regardless of column count — the `O(1)` streaming invariant holds across all schema widths.
- **Time scales linearly** with columns × rows: doubling columns roughly doubles serialization time at any given row count.
- The `SqlMetaData[]` schema definition is `static readonly` — shared across all invocations, never reallocated.
  Switching from a 3-column to a 10-column schema does **not** add any per-call schema allocation cost.
- Practical implication: if you need to insert wide rows frequently, the time cost is proportionally higher,
  but the memory cost remains flat — CaeriusNet never materialises a secondary buffer regardless of width.