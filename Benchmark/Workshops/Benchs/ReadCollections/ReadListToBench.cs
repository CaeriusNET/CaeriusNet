using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

/// <summary>
///     Measures sequential-read throughput of <see cref="List{T}" /> across five row-count scales
///     (1 → 100 000), using a realistic 5-field DTO to approximate production result-set payloads.
/// </summary>
/// <remarks>
///     Two traversal strategies are contrasted:
///     <list type="bullet">
///       <item><term>foreach (baseline)</term><description>
///           Direct iteration; no delegate allocation; mirrors the inner loop emitted by the
///           CaeriusNet source generator for <c>MapFromDataReader()</c>.
///       </description></item>
///       <item><term>LINQ Sum</term><description>
///           Delegate-based path that goes through <see cref="IEnumerable{T}"/> virtual dispatch;
///           the Ratio column quantifies the real overhead vs raw foreach.
///       </description></item>
///     </list>
///     Data is generated once per <see cref="RowCount"/> value with a fixed seed (42) to guarantee
///     reproducibility across runs and CI environments.
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ReadListToBench
{
    private List<BenchmarkItemDto> _data = null!;

    /// <summary>Number of elements in the collection under test.</summary>
    [Params(1, 100, 1_000, 10_000, 100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        _data = new List<BenchmarkItemDto>(RowCount);
        for (var i = 0; i < RowCount; i++)
            _data.Add(new BenchmarkItemDto(
                rng.Next(1, 1_000_000),
                Guid.NewGuid(),
                $"item_{i:D6}",
                Math.Round((decimal)(rng.NextDouble() * 9999.99), 2),
                i % 2 == 0));
    }

    /// <summary>Direct foreach — zero delegate overhead; mirrors generated reader-loop pattern.</summary>
    [Benchmark(Baseline = true, Description = "foreach — accumulate Sum(Id)")]
    public int Read_ForEach()
    {
        var sum = 0;
        foreach (var item in _data)
            sum += item.Id;
        return sum;
    }

    /// <summary>LINQ Sum — adds a delegate + IEnumerable virtual dispatch per element.</summary>
    [Benchmark(Description = "LINQ .Sum(item => item.Id)")]
    public int Read_LinqSum()
    {
        return _data.Sum(item => item.Id);
    }
}
