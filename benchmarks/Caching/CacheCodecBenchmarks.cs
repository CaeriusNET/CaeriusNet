using CaeriusNet.Benchmark.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CaeriusNet.Benchmark.Caching;

[MemoryDiagnoser]
[WarmupCount(3)]
[IterationCount(10)]
[BenchmarkCategory("cache", "codec")]
public class CacheCodecBenchmarks
{
    private static readonly BenchmarkDto MissValue = new(
        -1,
        "cache-miss",
        0m,
        DateTimeOffset.UnixEpoch,
        false,
        null);

    private readonly Dictionary<string, byte[]> _payloads = new(StringComparer.Ordinal);
    private BenchmarkDtoCaeriusCacheCodec _codec = null!;
    private ImmutableArray<BenchmarkDto> _collection;
    private ICaeriusCacheCodec<ImmutableArray<BenchmarkDto>> _collectionCodec = null!;
    private byte[] _collectionPayload = null!;
    private IFrozenCacheManager _frozen = null!;
    private IInMemoryCacheManager _memory = null!;
    private ServiceProvider _services = null!;
    private BenchmarkDto _value = null!;

    /// <summary>
    ///     Exercises the framing path at the small, normal and deliberately large result-set sizes
    ///     required by the cache acceptance matrix. Setup is intentionally outside the measured path.
    /// </summary>
    [Params(1, 1_000, 100_000)]
    public int CollectionSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _codec = new BenchmarkDtoCaeriusCacheCodec();
        _value = new BenchmarkDto(
            7,
            "cache-row",
            45.67m,
            new DateTimeOffset(2026, 8, 14, 12, 30, 0, TimeSpan.Zero),
            true,
            null);
        _payloads["hit"] = Encode(_value);

        var services = new ServiceCollection();
        services.AddCaeriusNet(options => options.UseSqlServer("Server=benchmark.invalid;Database=CaeriusNet;"));
        services.AddCaeriusCacheCodec<BenchmarkDto, BenchmarkDtoCaeriusCacheCodec>();
        _services = services.BuildServiceProvider();
        _memory = _services.GetRequiredService<IInMemoryCacheManager>();
        _frozen = _services.GetRequiredService<IFrozenCacheManager>();
        _collectionCodec = _services.GetRequiredService<ICaeriusCacheCodec<ImmutableArray<BenchmarkDto>>>();
        _memory.SetEntry("memory-hit", _value, TimeSpan.FromMinutes(5));
        _frozen.ReplaceSnapshot(new[] { new KeyValuePair<string, BenchmarkDto>("frozen-hit", _value) });

        var rows = ImmutableArray.CreateBuilder<BenchmarkDto>(CollectionSize);
        for (var index = 0; index < CollectionSize; index++)
            rows.Add(new BenchmarkDto(
                index,
                "cache-row-" + index.ToString(CultureInfo.InvariantCulture),
                index + 0.25m,
                DateTimeOffset.UnixEpoch.AddMinutes(index),
                (index & 1) == 0,
                index % 4 == 0 ? null : "optional"));

        _collection = rows.MoveToImmutable();
        _collectionPayload = Encode(_collectionCodec, _collection);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _services.Dispose();
    }

    [Benchmark]
    public byte[] EncodeDto()
    {
        return Encode(_value);
    }

    [Benchmark(Baseline = true)]
    public BenchmarkDto CodecCacheHit()
    {
        return _codec.Decode(_payloads["hit"]);
    }

    /// <summary>Measures binary collection framing plus the reusable per-element temporary buffer.</summary>
    [Benchmark]
    public byte[] EncodeImmutableCollection()
    {
        return Encode(_collectionCodec, _collection);
    }

    /// <summary>Measures bounded collection decoding and ownership-safe immutable-array wrapping.</summary>
    [Benchmark]
    public ImmutableArray<BenchmarkDto> DecodeImmutableCollection()
    {
        return _collectionCodec.Decode(_collectionPayload);
    }

    /// <summary>Measures the actual injected memory-cache hit path, including key lookup and expiry checks.</summary>
    [Benchmark]
    public bool InMemoryCacheHit()
    {
        return _memory.TryGet<BenchmarkDto>("memory-hit", out _);
    }

    /// <summary>Measures a read from the immutable snapshot without rebuilding it on a miss.</summary>
    [Benchmark]
    public bool FrozenCacheSnapshotHit()
    {
        return _frozen.TryGet<BenchmarkDto>("frozen-hit", out _);
    }

    /// <summary>Measures a true read-through miss plus population, rather than only dictionary serialization.</summary>
    [Benchmark]
    public async ValueTask<BenchmarkDto> InMemoryCacheMissAndPopulate()
    {
        _memory.Remove("memory-miss");
        return await _memory.GetOrCreateAsync(
            "memory-miss",
            TimeSpan.FromMinutes(5),
            false,
            static _ => ValueTask.FromResult(MissValue));
    }

    /// <summary>Models a deterministic binary-cache miss followed by the required population.</summary>
    [Benchmark]
    public BenchmarkDto CodecCacheMissAndPopulate()
    {
        const string key = "miss";
        if (_payloads.TryGetValue(key, out var payload))
            return _codec.Decode(payload);

        _payloads[key] = Encode(_value);
        _payloads.Remove(key);
        return _value;
    }

    private byte[] Encode(BenchmarkDto value)
    {
        var writer = new ArrayBufferWriter<byte>();
        _codec.Encode(writer, value);
        return writer.WrittenSpan.ToArray();
    }

    private static byte[] Encode<T>(ICaeriusCacheCodec<T> codec, T value)
    {
        var writer = new ArrayBufferWriter<byte>();
        codec.Encode(writer, value);
        return writer.WrittenSpan.ToArray();
    }
}