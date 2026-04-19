using CaeriusNet.Benchmark.Data.Generated;
using Microsoft.Extensions.Caching.Memory;

namespace CaeriusNet.Benchmark.Workshops.Benchs.Cache;

/// <summary>
///     Benchmarks read, write, and miss-handling performance of <see cref="IMemoryCache" /> at
///     varying cache sizes — the data structure underpinning CaeriusNet's in-memory cache layer.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="IMemoryCache" /> is a concurrent, key-expiring cache backed by a
///         <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}" />.
///         Unlike <see cref="System.Collections.Frozen.FrozenDictionary{TKey,TValue}" />, it supports
///         TTL-based expiration, making it suitable for mutable, time-bound cache populations.
///     </para>
///     <list type="bullet">
///         <item>
///             <term>Cache hit — TryGetValue</term>
///             <description>
///                 All entries are pre-populated in GlobalSetup; every TryGetValue call succeeds.
///                 Measures the hot-path cost of the ConcurrentDictionary lookup + entry validation.
///             </description>
///         </item>
///         <item>
///             <term>Cache miss — TryGetValue</term>
///             <description>
///                 Looks up keys that were never stored.  Measures the cost of the miss path
///                 (hash lookup + no-entry branch), which determines GetOrCreate performance when the
///                 cache is cold.
///             </description>
///         </item>
///         <item>
///             <term>Cache write — Set with TTL</term>
///             <description>
///                 Stores a single entry with a 5-minute absolute expiration, mirroring CaeriusNet's
///                 default TTL.  Measures allocation cost of MemoryCacheEntry + ConcurrentDictionary insert.
///             </description>
///         </item>
///         <item>
///             <term>GetOrCreate round-trip</term>
///             <description>
///                 The idiomatic MemoryCache usage pattern: atomically check + populate if missing.
///                 On a warm cache every call short-circuits to the hit path.  Ratio vs the pure hit
///                 benchmark shows the overhead of the GetOrCreate wrapper vs a raw TryGetValue.
///             </description>
///         </item>
///     </list>
///     <para>
///         Data is generated with a fixed seed (42) so results are reproducible across runs.
///         Cache sizes from 100 to 10 000 entries cover realistic hot-cache populations.
///     </para>
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class InMemoryCacheBench
{
    private IMemoryCache _cache = null!;
    private BenchmarkItemDto[] _entries = null!;
    private string[] _hitKeys = null!;
    private string[] _missKeys = null!;

    /// <summary>Number of entries pre-populated in the cache.</summary>
    [Params(100, 1_000, 10_000)]
    public int CacheSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _entries = new BenchmarkItemDto[CacheSize];
        _hitKeys = new string[CacheSize];
        _missKeys = new string[CacheSize];

        for (var i = 0; i < CacheSize; i++)
        {
            _hitKeys[i] = $"entity:hit:{i:D6}";
            _missKeys[i] = $"entity:miss:{i:D6}";
            _entries[i] = new BenchmarkItemDto(
                rng.Next(1, 1_000_000),
                Guid.NewGuid(),
                $"item_{i:D6}",
                Math.Round((decimal)(rng.NextDouble() * 9999.99), 2),
                i % 2 == 0);

            _cache.Set(_hitKeys[i], _entries[i], TimeSpan.FromMinutes(5));
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cache.Dispose();
    }

    /// <summary>
    ///     Cache hit: TryGetValue on all pre-populated keys.
    ///     Measures the steady-state read throughput of IMemoryCache when fully warm.
    /// </summary>
    [Benchmark(Baseline = true, Description = "IMemoryCache: TryGetValue — warm cache hit all keys")]
    public int Read_CacheHit_AllKeys()
    {
        var hits = 0;
        for (var i = 0; i < _hitKeys.Length; i++)
            if (_cache.TryGetValue(_hitKeys[i], out BenchmarkItemDto? _))
                hits++;
        return hits;
    }

    /// <summary>
    ///     Cache miss: TryGetValue on keys never stored.
    ///     Measures the miss-path cost (hash lookup + no-entry branch) — critical for cold-start scenarios.
    /// </summary>
    [Benchmark(Description = "IMemoryCache: TryGetValue — cold cache miss all keys")]
    public int Read_CacheMiss_AllKeys()
    {
        var misses = 0;
        for (var i = 0; i < _missKeys.Length; i++)
            if (!_cache.TryGetValue(_missKeys[i], out BenchmarkItemDto? _))
                misses++;
        return misses;
    }

    /// <summary>
    ///     Cache write: stores a single entry with a 5-minute TTL.
    ///     Measures the allocation cost of MemoryCacheEntry + ConcurrentDictionary insert on the write path.
    /// </summary>
    [Benchmark(Description = "IMemoryCache: Set a single entry with 5-min TTL")]
    public void Write_SingleEntry_WithTtl()
    {
        _cache.Set("bench:write:probe", _entries[0], TimeSpan.FromMinutes(5));
    }

    /// <summary>
    ///     GetOrCreate round-trip on a warm cache: atomically check + return existing entry.
    ///     On a warm cache the factory is never invoked.  Ratio vs baseline shows the wrapper overhead.
    /// </summary>
    [Benchmark(Description = "IMemoryCache: GetOrCreate — warm cache (factory never called)")]
    public int ReadWrite_GetOrCreate_WarmCache()
    {
        var hits = 0;
        for (var i = 0; i < _hitKeys.Length; i++)
        {
            _cache.GetOrCreate(_hitKeys[i], entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return _entries[i];
            });
            hits++;
        }

        return hits;
    }
}