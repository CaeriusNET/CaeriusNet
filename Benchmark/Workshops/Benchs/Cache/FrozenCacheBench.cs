using System.Collections.Frozen;
using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.Cache;

/// <summary>
///     Benchmarks read and write performance of a <see cref="System.Collections.Frozen.FrozenDictionary{TKey,TValue}" />
///     at varying cache sizes — the data structure underpinning CaeriusNet's frozen cache layer.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="System.Collections.Frozen.FrozenDictionary{TKey,TValue}"/> is an immutable, read-optimised
///         hash map: after construction it is sealed and cannot be modified, but its lookup path is heavily
///         optimised by the JIT (inlined hash computation, no lock overhead, no resize path).
///         This benchmark models the two dominant operations in CaeriusNet's frozen cache:
///     </para>
///     <list type="bullet">
///       <item><term>Read — sequential scan</term><description>
///           Accesses every key in insertion order.  Exercises the dictionary's hot path and the
///           CPU's hardware prefetcher.  Baseline; represents the steady-state read throughput
///           once the cache is fully populated.
///       </description></item>
///       <item><term>Read — random access</term><description>
///           Accesses keys in a pseudo-random order (seeded).  Stresses the hash-lookup path and
///           produces more cache misses than sequential access.  Ratio vs sequential shows the
///           impact of access locality on FrozenDictionary lookup throughput.
///       </description></item>
///       <item><term>Write — full dictionary rebuild</term><description>
///           CaeriusNet's frozen cache must rebuild the entire <c>FrozenDictionary</c> on each
///           write (immutable-by-design).  This benchmark quantifies that O(N) rebuild cost and
///           demonstrates why the frozen cache is optimised for write-once / read-many workloads.
///       </description></item>
///     </list>
///     <para>
///         Data is generated with a fixed seed (42) so results are reproducible.
///         Cache sizes from 100 to 10 000 keys cover realistic cache populations.
///     </para>
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class FrozenCacheBench
{
    private FrozenDictionary<string, BenchmarkItemDto> _cache = null!;
    private string[] _keys = null!;
    private string[] _randomKeys = null!;
    private KeyValuePair<string, BenchmarkItemDto>[] _sourceEntries = null!;

    /// <summary>Number of entries pre-populated in the frozen dictionary.</summary>
    [Params(100, 1_000, 10_000)]
    public int CacheSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        _keys = new string[CacheSize];
        _sourceEntries = new KeyValuePair<string, BenchmarkItemDto>[CacheSize];

        for (var i = 0; i < CacheSize; i++)
        {
            var key = $"entity:{i:D6}";
            _keys[i] = key;
            _sourceEntries[i] = new KeyValuePair<string, BenchmarkItemDto>(
                key,
                new BenchmarkItemDto(
                    rng.Next(1, 1_000_000),
                    Guid.NewGuid(),
                    $"item_{i:D6}",
                    Math.Round((decimal)(rng.NextDouble() * 9999.99), 2),
                    i % 2 == 0));
        }

        _cache = _sourceEntries.ToFrozenDictionary();

        // Pre-compute a shuffled key access order for the random-access benchmark
        _randomKeys = _keys.ToArray();
        for (var i = _randomKeys.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (_randomKeys[i], _randomKeys[j]) = (_randomKeys[j], _randomKeys[i]);
        }
    }

    /// <summary>
    ///     Sequential read: accesses all <see cref="CacheSize"/> entries in insertion order.
    ///     Favours the CPU hardware prefetcher — best-case FrozenDictionary read throughput.
    /// </summary>
    [Benchmark(Baseline = true, Description = "FrozenDictionary: sequential TryGetValue all keys")]
    public int Read_Sequential_AllKeys()
    {
        var hits = 0;
        for (var i = 0; i < _keys.Length; i++)
            if (_cache.TryGetValue(_keys[i], out _))
                hits++;
        return hits;
    }

    /// <summary>
    ///     Random read: accesses all <see cref="CacheSize"/> entries in a shuffled order.
    ///     Stresses hash-lookup path and increases cache-miss rate vs sequential access.
    /// </summary>
    [Benchmark(Description = "FrozenDictionary: random-order TryGetValue all keys")]
    public int Read_Random_AllKeys()
    {
        var hits = 0;
        for (var i = 0; i < _randomKeys.Length; i++)
            if (_cache.TryGetValue(_randomKeys[i], out _))
                hits++;
        return hits;
    }

    /// <summary>
    ///     Full dictionary rebuild: constructs a new <see cref="FrozenDictionary{TKey,TValue}"/>
    ///     from all source entries.  Models CaeriusNet's frozen cache write path (O(N) per write).
    ///     At large CacheSize this is visibly expensive — by design, writes should be infrequent.
    /// </summary>
    [Benchmark(Description = "FrozenDictionary: full rebuild from source entries")]
    public FrozenDictionary<string, BenchmarkItemDto> Write_FullRebuild()
    {
        return _sourceEntries.ToFrozenDictionary();
    }
}
