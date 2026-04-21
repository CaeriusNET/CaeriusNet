---
title: Collection Benchmarks
description: CaeriusNet collection benchmarks — read and create performance of List<T>, ReadOnlyCollection<T>, IEnumerable<T>, ImmutableArray<T>, and List<T> capacity pre-allocation strategies.
---

# Collection Benchmarks

These benchmarks compare the performance of different .NET collection types for **reading** and **creating**
result sets returned by CaeriusNet, and the impact of `List<T>` capacity pre-allocation.

Understanding these numbers helps developers choose the right return type for their use case —
the choice between `IReadOnlyList<T>`, `IEnumerable<T>`, and `ImmutableArray<T>` has measurable consequences
for throughput and GC pressure under load.

> All read and create benchmarks use `[Params(1, 100, 1_000, 10_000, 100_000)]` on `RowCount`.
> ListCapacity benchmarks use `[Params(100, 1_000, 10_000, 100_000)]` (sizes ≥ 100 are meaningful for capacity decisions).
> See [Methodology & Overview](./index) for BDN configuration details.

---

## Reading Collections

These benchmarks model the **consumer side** of CaeriusNet: after mapping rows into a typed collection,
how efficiently can downstream code iterate that collection?

Each read benchmark exposes two methods:

| Method | Pattern |
|---|---|
| `Read_ForEach` *(Baseline)* | `foreach` loop — uses the collection's own enumerator |
| `Read_LinqSum` | `collection.Sum(x => x.Id)` — LINQ aggregation through the `IEnumerable<T>` interface |

### Memory layout and cache locality

The fundamental performance difference between collection types lies in **how elements are laid out in memory**
and **what the enumerator does at each step**:

| Type | Memory layout | Enumerator |
|---|---|---|
| `List<T>` | Contiguous `T[]` internal array | Struct enumerator, increments an index — zero heap allocation |
| `ReadOnlyCollection<T>` | Wraps a `List<T>` or `IList<T>` — same contiguous array | Interface `IEnumerator<T>` — virtual dispatch per `MoveNext` |
| `IEnumerable<T>` | Depends on the underlying source; may be an iterator state machine | Virtual dispatch per `MoveNext`; possible heap allocation for the enumerator |
| `ImmutableArray<T>` | Contiguous `T[]` internal array (sealed, value-type wrapper) | **Struct enumerator** on the `ImmutableArray<T>` type itself — zero virtual dispatch, zero heap allocation |

> `ImmutableArray<T>` has the same memory layout as `T[]` but wraps it in a value type, so iterating it
> via `foreach` uses the struct enumerator (no boxing, no virtual call). This makes it the fastest collection
> type for read-only, cache-friendly iteration when the full array is traversed sequentially.

### ReadListToBench

**Benchmark class: `ReadListToBench`**

Iterates a `List<BenchmarkItemDto>` using `foreach` and via `LINQ Sum`.
`List<T>` uses a struct enumerator internally — `foreach` compiles to a direct index-loop with no virtual dispatch.

---

### ReadReadOnlyCollectionToBench

**Benchmark class: `ReadReadOnlyCollectionToBench`**

Iterates a `ReadOnlyCollection<BenchmarkItemDto>` (returned by `.AsReadOnly()` on a `List<T>`).
`ReadOnlyCollection<T>` implements `IEnumerable<T>` via explicit interface — `foreach` on the concrete type
still dispatches through the virtual `IEnumerator<T>` interface, adding one virtual call per element vs `List<T>`.

---

### ReadEnumerableToBench

**Benchmark class: `ReadEnumerableToBench`**

Iterates an `IEnumerable<BenchmarkItemDto>` backed by a `List<T>` cast to the interface.
Every `MoveNext` and `Current` access goes through virtual dispatch.
At large row counts the overhead accumulates into a measurable Ratio vs the `List<T>` baseline.

---

### ReadImmutableArrayToBench

**Benchmark class: `ReadImmutableArrayToBench`**

Iterates an `ImmutableArray<BenchmarkItemDto>`. The `foreach` over `ImmutableArray<T>` uses its own
**value-type struct enumerator**, which the JIT inlines directly — no virtual call, no heap allocation.
For sequential full-traversal workloads, this is the most cache-friendly option.

---

## Creating Collections

These benchmarks model the **producer side** of CaeriusNet: after streaming rows from `SqlDataReader`,
how efficiently can a typed collection be constructed?

Each create benchmark compares two strategies:

| Strategy | Characteristic |
|---|---|
| Method A *(Baseline)* | Uses `new List<T>(capacity)` + `AddRange` — explicit pre-allocation, single array copy |
| Method B | Uses `source.ToList()` or `ImmutableArray.Create()` — relies on internal copy/builder paths |

### CreateListToBench

**Benchmark class: `CreateListToBench`**

Compares `new List<T>(capacity) { AddRange(source) }` vs `source.ToList()`.

- `new List<T>(capacity)`: pre-allocates the internal array once, then `AddRange` does a single `Array.Copy`.
  No internal resize, O(N) time, O(N) allocation.
- `source.ToList()`: LINQ internally creates a `List<T>` without a capacity hint, triggering logarithmic
  resize steps (×2 growth) until the source is exhausted.

---

### CreateReadOnlyCollectionToBench

**Benchmark class: `CreateReadOnlyCollectionToBench`**

Compares `new List<T>(capacity) { AddRange } .AsReadOnly()` vs `source.ToList().AsReadOnly()`.

`.AsReadOnly()` is a zero-copy wrapper — it allocates one `ReadOnlyCollection<T>` object that references the
existing `List<T>` internal array. The creation cost difference is therefore identical to `CreateListToBench`,
plus a fixed O(1) wrapper allocation.

---

### CreateEnumerableToBench

**Benchmark class: `CreateEnumerableToBench`**

Compares two lazy strategies for producing `IEnumerable<T>`:

- **Materialise + cast** (`new List<T>(capacity).AsEnumerable()`): pays O(N) upfront to build the list,
  then returns a zero-copy interface cast. Consumers pay zero additional allocation on enumeration.
- **Array + cast** (`source.ToArray().AsEnumerable()`): uses `Array.Copy` for a compact, read-only backing store.
  Useful when the element count is known and the consumer only ever iterates (never indexes).

---

### CreateImmutableArrayToBench

**Benchmark class: `CreateImmutableArrayToBench`**

Compares two `ImmutableArray<T>` construction paths:

- **Builder pattern** (`ImmutableArray.CreateBuilder<T>(capacity).MoveToImmutable()`): pre-allocates a mutable
  builder, fills it, then transfers ownership — **zero-copy** transition to the immutable array.
  `MoveToImmutable()` is valid only when `builder.Capacity == builder.Count` (exact capacity).
- **`ImmutableArray.Create(ReadOnlySpan<T>)`**: copies from a `Span<T>` (e.g., a stack-allocated or array slice).
  Ideal when the source data is already in a contiguous buffer.

---

## List Capacity Pre-Allocation

These benchmarks measure the performance impact of the `List<T>` capacity hint parameter.
The four classes together form a **cross-class comparison set** — use the `Ratio` column relative to
`ListWithCapacityToBench` (exact capacity, the ideal baseline) to understand the overhead of each variant.

### Capacity strategies compared

| Class | Strategy | Expected resize count |
|---|---|---|
| `ListWithCapacityToBench` | `new List<T>(N)` — exact capacity | 0 (no resize) |
| `ListWithoutCapacityToBench` | `new List<T>()` — no capacity hint | ⌈log₂(N)⌉ resize steps |
| `ListWithCapacityWithOverextendToBench` | `new List<T>(N)` then adds 2N items | 1 resize (exactly doubles once) |
| `ListWithLessCapacityThanNeededToBench` | `new List<T>(N/2)` then adds N items | 1 resize at N/2 boundary |

### How List resize works

When `List<T>` runs out of capacity, it:
1. Allocates a new array of capacity × 2.
2. Copies all existing elements via `Array.Copy`.
3. Releases the old array (eligible for GC).

Each resize is O(capacity) in both time and allocation. With no capacity hint (`new List<T>()`), starting from
an empty list and adding N elements triggers ⌈log₂(N)⌉ resizes, which means at N = 100 000, the internal array
is reallocated ~17 times and the total number of element copies is ~2N.

### ListWithCapacityToBench

**Benchmark class: `ListWithCapacityToBench`**

Exact capacity — no resize. This is the **ideal baseline** for all capacity comparisons.

---

### ListWithoutCapacityToBench

**Benchmark class: `ListWithoutCapacityToBench`**

No capacity hint — `List<T>()` default. Triggers O(log N) resize steps.
At large RowCount values, the time and allocation overhead vs the exact-capacity case becomes significant.

---

### ListWithCapacityWithOverextendToBench

**Benchmark class: `ListWithCapacityWithOverextendToBench`**

Capacity is set to N, but 2N items are added — forces exactly one resize at the N boundary.
This models the scenario where the estimated row count from SQL is correct but additional items are appended later.

---

### ListWithLessCapacityThanNeededToBench

**Benchmark class: `ListWithLessCapacityThanNeededToBench`**

Capacity is set to N/2, but N items are added — forces one resize at the N/2 boundary.
This models an underestimated capacity hint (e.g., using the previous page's row count for the current page).

---

## Collection Type Recommendations

Based on the benchmark results above, use the following decision guide:

| Scenario | Recommended type | Reason |
|---|---|---|
| Read-only result, full sequential scan | `ImmutableArray<T>` | Struct enumerator, contiguous layout, no virtual dispatch |
| Mutable result builder, then consume | `List<T>` with capacity hint | Pre-allocation eliminates resize steps |
| Public API surface (read-only contract) | `IReadOnlyList<T>` | Interface allows `List<T>` backing, preserves indexing |
| Lazy pipeline, not always materialised | `IEnumerable<T>` | Only pay materialisation cost if consumer iterates |
| Wrapping an existing list as read-only | `ReadOnlyCollection<T>` | Zero-copy `.AsReadOnly()` wrapper |
