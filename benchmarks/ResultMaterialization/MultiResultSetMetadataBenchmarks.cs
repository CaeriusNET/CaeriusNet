using CaeriusNet.Benchmark.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CaeriusNet.Benchmark.ResultMaterialization;

/// <summary>
///     Isolates the generated multi-result metadata hot path. The runtime APIs require a live SQL Server reader, so this
///     benchmark deliberately measures the allocation-sensitive portion independently: the former per-call arrays and
///     shape construction against the generic-static-holder pattern now emitted by the build-only generator.
/// </summary>
[MemoryDiagnoser]
[WarmupCount(3)]
[IterationCount(10)]
[BenchmarkCategory("multi-result", "metadata", "cache")]
public class MultiResultSetMetadataBenchmarks
{
    private readonly ServiceCollection _serviceCollection = new();
    private IFrozenCacheManager _frozen = null!;
    private IInMemoryCacheManager _memory = null!;
    private ServiceProvider _services = null!;

    [Params(2, 5, 10)] public int ResultSetCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _serviceCollection.AddCaeriusNet(options =>
            options.UseSqlServer("Server=benchmark.invalid;Database=CaeriusNet;"));
        _services = _serviceCollection.BuildServiceProvider();
        _memory = _services.GetRequiredService<IInMemoryCacheManager>();
        _frozen = _services.GetRequiredService<IFrozenCacheManager>();

        foreach (var plan in StaticPlans.All) _memory.SetEntry(plan.Shape, plan, TimeSpan.FromMinutes(5));
        _frozen.ReplaceSnapshot(StaticPlans.All.Select(static plan =>
            new KeyValuePair<string, MultiResultMetadata>(plan.Shape, plan)));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _services.Dispose();
    }

    /// <summary>
    ///     Represents the previous generated invocation shape: capacities, readers, factories, result types and a joined
    ///     type shape.
    /// </summary>
    [Benchmark(Baseline = true)]
    public MultiResultMetadata DynamicPerCallMetadata()
    {
        return ResultSetCount switch
        {
            2 => CreateDynamicMetadata(2),
            5 => CreateDynamicMetadata(5),
            10 => CreateDynamicMetadata(10),
            _ => throw new InvalidOperationException()
        };
    }

    /// <summary>Reads the equivalent generic-static metadata holder with no cache lookup.</summary>
    [Benchmark]
    public MultiResultMetadata StaticGeneratedPlanMetadata()
    {
        return ResultSetCount switch
        {
            2 => StaticPlans.Two,
            5 => StaticPlans.Five,
            10 => StaticPlans.Ten,
            _ => throw new InvalidOperationException()
        };
    }

    [Benchmark]
    public bool InMemoryCacheHit()
    {
        var plan = StaticGeneratedPlanMetadata();
        return _memory.TryGet<MultiResultMetadata>(plan.Shape, out _);
    }

    [Benchmark]
    public bool FrozenCacheHit()
    {
        var plan = StaticGeneratedPlanMetadata();
        return _frozen.TryGet<MultiResultMetadata>(plan.Shape, out _);
    }

    private static MultiResultMetadata CreateDynamicMetadata(int arity)
    {
        var capacities = new int[arity];
        var readers = new Func<object>[arity];
        var emptyFactories = new Func<object>[arity];
        var resultTypes = new Type[arity];
        var typeNames = new string[arity];
        var elementTypeName = typeof(BenchmarkDto).FullName ?? typeof(BenchmarkDto).Name;
        for (var index = 0; index < arity; index++)
        {
            capacities[index] = 16;
            readers[index] = static () => Array.Empty<BenchmarkDto>();
            emptyFactories[index] = static () => Array.Empty<BenchmarkDto>();
            resultTypes[index] = typeof(BenchmarkDto[]);
            typeNames[index] = elementTypeName;
        }

        return new MultiResultMetadata(capacities, readers, emptyFactories, resultTypes,
            "enumerable:" + arity.ToString(CultureInfo.InvariantCulture) + ':' + string.Join("|", typeNames));
    }

    public sealed record MultiResultMetadata(
        int[] Capacities,
        Func<object>[] Readers,
        Func<object>[] EmptyFactories,
        Type[] ResultTypes,
        string Shape);

    private static class StaticPlans
    {
        internal static readonly MultiResultMetadata Two = CreateStatic(2);
        internal static readonly MultiResultMetadata Five = CreateStatic(5);
        internal static readonly MultiResultMetadata Ten = CreateStatic(10);
        internal static readonly MultiResultMetadata[] All = [Two, Five, Ten];

        private static MultiResultMetadata CreateStatic(int arity)
        {
            var capacities = Enumerable.Repeat(16, arity).ToArray();
            var readers = Enumerable.Repeat<Func<object>>(static () => Array.Empty<BenchmarkDto>(), arity).ToArray();
            var emptyFactories = Enumerable.Repeat<Func<object>>(static () => Array.Empty<BenchmarkDto>(), arity)
                .ToArray();
            var resultTypes = Enumerable.Repeat(typeof(BenchmarkDto[]), arity).ToArray();
            var typeName = typeof(BenchmarkDto).FullName ?? typeof(BenchmarkDto).Name;
            return new MultiResultMetadata(capacities, readers, emptyFactories, resultTypes,
                "enumerable:" + arity.ToString(CultureInfo.InvariantCulture) + ':' +
                string.Join("|", Enumerable.Repeat(typeName, arity)));
        }
    }
}
