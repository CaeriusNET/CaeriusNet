using CaeriusNet.Benchmark.Infrastructure;
using CaeriusNet.Execution;

namespace CaeriusNet.Benchmark.Caching;

/// <summary>
///     Measures the old capturing call site against the stateful executor overload under the same populated cache key.
///     The comparison is intentionally isolated from codec/collection benchmarks so BenchmarkDotNet can use one
///     baseline for this allocation-sensitive hot path.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("cache", "executor")]
public class CachedAsyncMemoryHitBenchmarks
{
    private StoredProcedureCommand _command = null!;
    private ICaeriusExecutionContext _context = null!;
    private IServiceScope _scope = null!;
    private ServiceProvider _services = null!;
    private BenchmarkDto _value = null!;

    [GlobalSetup]
    public void Setup()
    {
        _value = new BenchmarkDto(
            7,
            "cache-row",
            45.67m,
            new DateTimeOffset(2026, 8, 14, 12, 30, 0, TimeSpan.Zero),
            true,
            null);
        var services = new ServiceCollection();
        services.AddCaeriusNet(options => options.UseSqlServer("Server=benchmark.invalid;Database=CaeriusNet;"));
        services.AddCaeriusCacheCodec<BenchmarkDto, BenchmarkDtoCaeriusCacheCodec>();
        _services = services.BuildServiceProvider();
        _scope = _services.CreateScope();
        _context = (ICaeriusExecutionContext)_scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        _command = new StoredProcedureCommandBuilder("dbo", "BenchmarkCacheHit")
            .UseInMemoryCache(TimeSpan.FromMinutes(5))
            .Build();

        var key = CaeriusCacheKeyBuilder.Create(
            _context.Options.ApplicationNamespace,
            _context.Options.CacheDatabaseIdentity ?? _context.Name,
            _command,
            "benchmark:dto",
            _context.Codecs.OfType<ICaeriusCacheCodec<BenchmarkDto>>().Single().ContractHash);
        _context.MemoryCache.SetEntry(key, _value, TimeSpan.FromMinutes(5));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.Dispose();
        _services.Dispose();
    }

    /// <summary>Closure-based form: the result value is captured once per invocation.</summary>
    [Benchmark(Baseline = true)]
    public ValueTask<BenchmarkDto> CapturingFactory()
    {
        return CaeriusExecutor.CachedAsync(
            _context,
            _command,
            "benchmark:dto",
            _ => ValueTask.FromResult(_value),
            CancellationToken.None);
    }

    /// <summary>Static factory plus typed state: no closure and no boxed state on the synchronous memory-hit path.</summary>
    [Benchmark]
    public ValueTask<BenchmarkDto> StatefulFactory()
    {
        return CaeriusExecutor.CachedAsync<BenchmarkDto, BenchmarkDto>(
            _context,
            _command,
            "benchmark:dto",
            _value,
            static (_, state, _) => ValueTask.FromResult(state),
            CancellationToken.None);
    }
}
