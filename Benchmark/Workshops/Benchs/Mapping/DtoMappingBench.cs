using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.Mapping;

/// <summary>
///     Benchmarks the overhead of DTO construction patterns used by the source-generated mapper.
///     Since SqlDataReader cannot be instantiated without a live connection, we benchmark
///     the construction path (ordinal-based positional init) vs named property assignment.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.CacheMisses)]
public class DtoMappingBench
{
    private static readonly Faker<BenchmarkItemDto> Faker = new Faker<BenchmarkItemDto>()
        .CustomInstantiator(f => new BenchmarkItemDto(
            f.Random.Int(1, 100_000),
            f.Random.Guid(),
            f.Internet.UserName(),
            Math.Round((decimal)f.Random.Double(0.01, 9999.99), 2),
            f.Random.Bool()));

    private int[] _ids = null!;
    private bool[] _isActives = null!;
    private string[] _names = null!;
    private decimal[] _prices = null!;
    private Guid[] _traceIds = null!;

    [Params(1, 100, 1_000, 10_000, 100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _ids = new int[RowCount];
        _traceIds = new Guid[RowCount];
        _names = new string[RowCount];
        _prices = new decimal[RowCount];
        _isActives = new bool[RowCount];

        for (var i = 0; i < RowCount; i++)
        {
            _ids[i] = random.Next(1, 100_000);
            _traceIds[i] = Guid.NewGuid();
            _names[i] = $"user_{i}";
            _prices[i] = Math.Round((decimal)(random.NextDouble() * 9999), 2);
            _isActives[i] = i % 2 == 0;
        }
    }

    /// <summary>Positional constructor init — mirrors the source-generated MapFromDataReader pattern.</summary>
    [Benchmark(Baseline = true, Description = "Generated mapper pattern (positional ctor)")]
    public List<BenchmarkItemDto> Map_Via_PositionalConstructor()
    {
        var result = new List<BenchmarkItemDto>(RowCount);
        for (var i = 0; i < RowCount; i++)
            result.Add(new BenchmarkItemDto(_ids[i], _traceIds[i], _names[i], _prices[i], _isActives[i]));
        return result;
    }

    /// <summary>Property setter init — the naive manual mapping pattern.</summary>
    [Benchmark(Description = "Manual mapper pattern (property setters)")]
    public List<BenchmarkItemDto> Map_Via_PropertySetters()
    {
        var result = new List<BenchmarkItemDto>(RowCount);
        for (var i = 0; i < RowCount; i++)
            result.Add(new BenchmarkItemDto(_ids[i], _traceIds[i], _names[i], _prices[i], _isActives[i]) with { });
        return result;
    }

    /// <summary>Pre-allocated span-based loop — optimal allocation pattern.</summary>
    [Benchmark(Description = "Span-based pre-allocated array")]
    public BenchmarkItemDto[] Map_Via_PreAllocatedArray()
    {
        var result = new BenchmarkItemDto[RowCount];
        for (var i = 0; i < RowCount; i++)
            result[i] = new BenchmarkItemDto(_ids[i], _traceIds[i], _names[i], _prices[i], _isActives[i]);
        return result;
    }
}