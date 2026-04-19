using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

/// <summary>
///     Measures sequential-read throughput of <see cref="ReadOnlyCollection{T}" /> across five
///     row-count scales (1 → 100 000).
/// </summary>
/// <remarks>
///     <see cref="ReadOnlyCollection{T}" /> is a thin reference wrapper over an <see cref="IList{T}" />.
///     Iteration goes through the <c>IList</c> virtual interface, which prevents JIT devirtualisation.
///     Comparing its Ratio against <see cref="ReadListToBench" /> quantifies the overhead introduced
///     by the read-only wrapper in tight iteration loops.
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ReadReadOnlyCollectionToBench
{
    private ReadOnlyCollection<BenchmarkItemDto> _data = null!;

    /// <summary>Number of elements in the collection under test.</summary>
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
        _data = list.AsReadOnly();
    }

    /// <summary>Direct foreach — enumerator obtained via IList virtual call; no boxing.</summary>
    [Benchmark(Baseline = true, Description = "foreach — accumulate Sum(Id)")]
    public int Read_ForEach()
    {
        var sum = 0;
        foreach (var item in _data)
            sum += item.Id;
        return sum;
    }

    /// <summary>LINQ Sum — stacks a delegate on top of the virtual enumerator.</summary>
    [Benchmark(Description = "LINQ .Sum(item => item.Id)")]
    public int Read_LinqSum()
    {
        return _data.Sum(item => item.Id);
    }
}