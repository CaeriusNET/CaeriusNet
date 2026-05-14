using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections;

/// <summary>
///     Measures the cost of materialising a <see cref="List{T}" /> from a pre-existing source array
///     across five row-count scales (1 → 100 000).
/// </summary>
/// <remarks>
///     Two construction paths are compared:
///     <list type="bullet">
///         <item>
///             <term>Explicit capacity + AddRange (baseline)</term>
///             <description>
///                 Sets capacity to <see cref="RowCount" /> so the internal array is allocated once and
///                 <c>AddRange</c> performs a single memcopy — no resize occurs.
///             </description>
///         </item>
///         <item>
///             <term>LINQ ToList</term>
///             <description>
///                 Internally creates a <see cref="List{T}" /> without a hint and fills it via the
///                 <see cref="IEnumerable{T}" /> path, which may trigger one extra reallocation step.
///             </description>
///         </item>
///     </list>
///     The Allocated column reveals the memory difference between both strategies at each scale.
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class CreateListToBench
{
    private BenchmarkItemDto[] _source = null!;

    /// <summary>Number of items to materialise per benchmark call.</summary>
    [Params(1, 100, 1_000, 10_000, 100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        _source = new BenchmarkItemDto[RowCount];
        for (var i = 0; i < RowCount; i++)
            _source[i] = new BenchmarkItemDto(
                rng.Next(1, 1_000_000),
                Guid.NewGuid(),
                $"item_{i:D6}",
                Math.Round((decimal)(rng.NextDouble() * 9999.99), 2),
                i % 2 == 0);
    }

    /// <summary>Exact capacity hint → single array allocation, single memcopy — zero resize overhead.</summary>
    [Benchmark(Baseline = true, Description = "new List<T>(RowCount) + AddRange — exact capacity")]
    public List<BenchmarkItemDto> Create_WithCapacity()
    {
        var list = new List<BenchmarkItemDto>(RowCount);
        list.AddRange(_source);
        return list;
    }

    /// <summary>LINQ ToList — convenient but may trigger an extra reallocation vs the explicit path.</summary>
    [Benchmark(Description = "source.ToList() — no capacity hint")]
    public List<BenchmarkItemDto> Create_LinqToList()
    {
        return _source.ToList();
    }
}
