using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

/// <summary>
///     Measures sequential-read throughput of <see cref="IEnumerable{T}" /> across five
///     row-count scales (1 → 100 000).
/// </summary>
/// <remarks>
///     The underlying storage is a <see cref="List{T}" /> exposed as <see cref="IEnumerable{T}" />.
///     This models the most common public API shape: a query method returning a lazy sequence.
///     Typed as an interface, the JIT cannot devirtualise the enumerator, adding measurable overhead
///     compared with direct <see cref="List{T}" /> access — visible in the Ratio column when cross-
///     referenced against <see cref="ReadListToBench" />.
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ReadEnumerableToBench
{
    private IEnumerable<BenchmarkItemDto> _data = null!;

    /// <summary>Number of elements in the sequence under test.</summary>
    [Params(1, 100, 1_000, 10_000, 100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        var list = new List<BenchmarkItemDto>(RowCount);
        for (var i = 0; i < RowCount; i++)
            list.Add(new BenchmarkItemDto(
                rng.Next(1, 1_000_000),
                Guid.NewGuid(),
                $"item_{i:D6}",
                Math.Round((decimal)(rng.NextDouble() * 9999.99), 2),
                i % 2 == 0));
        _data = list; // List stored as IEnumerable<T> — prevents devirtualisation
    }

    /// <summary>foreach via IEnumerable — virtual dispatch blocks JIT devirtualisation of enumerator.</summary>
    [Benchmark(Baseline = true, Description = "foreach via IEnumerable<T> — accumulate Sum(Id)")]
    public int Read_ForEach()
    {
        var sum = 0;
        foreach (var item in _data)
            sum += item.Id;
        return sum;
    }

    /// <summary>LINQ Sum — adds a delegate on top of the interface-dispatch chain.</summary>
    [Benchmark(Description = "LINQ .Sum(item => item.Id)")]
    public int Read_LinqSum()
    {
        return _data.Sum(item => item.Id);
    }
}