---
title: Cache Benchmarks
description: CaeriusNet cache benchmarks — FrozenDictionary read/write throughput and IMemoryCache hit, miss, Set, and GetOrCreate performance at varying cache sizes.
---

# Cache Benchmarks

These benchmarks measure the read and write throughput of CaeriusNet's two cache layers:

| Layer | Implementation | Characteristic |
|---|---|---|
| **Frozen Cache** | `System.Collections.Frozen.FrozenDictionary<K,V>` | Immutable after construction; read-optimised; no expiration |
| **In-Memory Cache** | `Microsoft.Extensions.Caching.Memory.IMemoryCache` | Mutable, concurrent; TTL-based expiration; write-friendly |

Both layers are benchmarked at `[Params(100, 1_000, 10_000)]` for `CacheSize`,
covering three realistic cache population sizes.
All data is generated with a fixed seed (42) for reproducibility.

---

## FrozenCache — FrozenDictionary Throughput

**Benchmark class: `FrozenCacheBench`**

### Architecture

`FrozenDictionary<TKey, TValue>` (introduced in .NET 8) is an immutable, read-optimised hash map
produced by calling `.ToFrozenDictionary()` on any `IEnumerable<KeyValuePair<K,V>>`.
Once constructed, it cannot be modified.

The .NET runtime optimises `FrozenDictionary` for lookup by:
1. Selecting a hash algorithm at construction time that minimises collisions for the actual set of keys stored.
2. Laying out buckets and values in contiguous memory arrays — maximising L1/L2 cache utilisation.
3. Generating specialised lookup code paths per key type (e.g., `string` uses a minimal perfect hash when the
   key count is small enough).

These properties make `FrozenDictionary` the fastest .NET hash map for **stable, read-heavy data** —
lookup throughput typically exceeds `Dictionary<K,V>` by 20–40 % because the absence of a write path
allows more aggressive layout and inline-hashing optimisations.

**Trade-off:** Every write (new key, updated value, removal) requires rebuilding the **entire** dictionary
from scratch via `.ToFrozenDictionary()`. This is an O(N) operation and allocates a new dictionary object.
The frozen cache is therefore optimised for **write-once / read-many** access patterns — typical for
configuration caches, static lookup tables, and reference data loaded at startup.

### Benchmark methods

| Method | Description |
|---|---|
| `Read_Sequential_AllKeys` *(Baseline)* | `TryGetValue` for all keys in insertion order — maximum hardware prefetcher benefit |
| `Read_Random_AllKeys` | `TryGetValue` for all keys in a shuffled order — stresses the hash-lookup path |
| `Write_FullRebuild` | `.ToFrozenDictionary()` from source entries — measures the O(N) write cost |

### Key insights

**Sequential vs random reads:**
- Sequential access (insertion order) allows the CPU's hardware prefetcher to predict the next memory address
  before it is needed, loading the bucket array into L1 cache ahead of each lookup.
  This yields the highest possible throughput for `FrozenDictionary`.
- Random access produces more L1/L2 cache misses (visible in the `CacheMisses` hardware counter if PMU is available).
  The Ratio between sequential and random quantifies the locality penalty.
- Even random-access throughput on `FrozenDictionary` is competitive with `Dictionary<K,V>` because the
  contiguous layout and minimal-hash algorithm reduce average probe length.

**Write — full rebuild:**
- The rebuild cost scales linearly with `CacheSize`: O(N) key hashing + O(N) layout optimisation.
- At `CacheSize = 10 000`, the rebuild time is measurably larger than a single lookup cycle —
  confirming that frozen cache writes should be batched and infrequent.
- The rebuild allocates a new `FrozenDictionary` instance + its internal arrays. The old instance becomes
  eligible for GC immediately after the reference is swapped (typically Gen1 or Gen2 depending on size).
- **Practical guidance:** Use the frozen cache for data that changes at most once per deployment or on
  a scheduled refresh cycle (e.g., every 5–60 minutes). For data that changes per-request, use `IMemoryCache`.

<!--@include: ./results/FrozenCacheBench.md-->

---

## InMemoryCache — IMemoryCache Throughput

**Benchmark class: `InMemoryCacheBench`**

### Architecture

`IMemoryCache` (from `Microsoft.Extensions.Caching.Memory`) is backed by a
`ConcurrentDictionary<object, CacheEntry>` with a background expiration scanner.
Unlike `FrozenDictionary`, it supports:
- **TTL-based expiration**: entries can have an absolute or sliding expiration date.
- **Concurrent writes**: multiple threads can add and evict entries simultaneously without external locking.
- **Eviction callbacks**: code can be notified when an entry is removed (timeout, manual, memory pressure).

These features come with a cost:
- Every `TryGetValue` acquires a read on the `ConcurrentDictionary` + validates the entry's expiration timestamp.
- Every `Set` allocates a `MemoryCacheEntry` object wrapping the key, value, and expiration metadata.
- The background expiration scanner adds periodic overhead under sustained write load.

### Benchmark methods

| Method | Description |
|---|---|
| `Read_CacheHit_AllKeys` *(Baseline)* | `TryGetValue` on all pre-populated keys — all succeed (warm cache) |
| `Read_CacheMiss_AllKeys` | `TryGetValue` on keys never stored — all fail (cold-cache path) |
| `Write_SingleEntry_WithTtl` | `Set(key, value, TimeSpan.FromMinutes(5))` — single entry write with TTL |
| `ReadWrite_GetOrCreate_WarmCache` | `GetOrCreate(key, factory)` on a warm cache — factory never invoked |

### Key insights

**Cache hit vs cache miss:**
- The **hit path** (`TryGetValue` succeeds): `ConcurrentDictionary` lookup + expiration timestamp validation.
  This is the steady-state cost for all reads on a warm cache.
- The **miss path** (`TryGetValue` fails): same `ConcurrentDictionary` lookup, but the key is absent.
  The miss path is marginally cheaper than the hit path because expiration validation is skipped.
- At large `CacheSize` (10 000 entries), both paths are dominated by `ConcurrentDictionary` hash-bucket
  probing. The Ratio between hit and miss reveals the cost of expiration validation.

**Write — Set with TTL:**
- Each `Set` allocates one `MemoryCacheEntry` on the heap, triggering a Gen0 GC collection proportionally
  with write frequency.
- The TTL metadata (absolute expiration `DateTimeOffset`) is stored per entry — not a shared structure.
  For workloads with millions of entries, this per-entry overhead becomes the dominant allocation source.
- **Practical guidance:** For write-heavy workloads with high entry turnover, consider grouping entries
  into fewer, coarser-grained cache keys to reduce `MemoryCacheEntry` churn.

**GetOrCreate on warm cache:**
- On a warm cache, `GetOrCreate` should be equivalent to a raw `TryGetValue` — the factory delegate
  is never invoked.
- The Ratio between `GetOrCreate_WarmCache` and `Read_CacheHit_AllKeys` shows the overhead of the
  `GetOrCreate` wrapper (delegate allocation + factory call check) vs a direct `TryGetValue`.
- A non-trivial Ratio here would indicate that `GetOrCreate` has overhead beyond a raw `TryGetValue`
  even when the entry already exists — relevant for hot-path code where every nanosecond counts.

**FrozenCache vs InMemoryCache:**
- For data that never changes (or changes very rarely), `FrozenDictionary` consistently outperforms
  `IMemoryCache` on reads because it has zero locking overhead, no expiration validation, and a
  contiguous memory layout optimised for the specific set of keys.
- For data that is updated regularly (per-minute or per-request), `IMemoryCache` is the correct choice:
  its ConcurrentDictionary backing allows lock-free writes from multiple threads simultaneously.
- The benchmark results on this page make the read-throughput gap between the two explicit at each cache size.

<!--@include: ./results/InMemoryCacheBench.md-->
