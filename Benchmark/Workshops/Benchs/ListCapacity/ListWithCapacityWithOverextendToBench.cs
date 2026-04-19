using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ListCapacity;

/// <summary>
///     Measures <see cref="List{T}" /> construction cost when the capacity hint equals
///     <see cref="RowCount"/> but exactly <c>RowCount × 2</c> items are added — forcing a single
///     resize beyond the initial hint.
/// </summary>
/// <remarks>
///     This models the scenario where the caller under-estimates the final item count by a factor
///     of two and the list must double its internal array exactly once.  The additional memcopy of
///     the initial allocation is visible in the Allocated column vs the exact-capacity baseline
///     (<see cref="ListWithCapacityToBench"/>).  The Ratio column isolates the cost of carrying one
///     forced resize at each scale from 1 to 100 000.
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ListWithCapacityWithOverextendToBench
{
    private BenchmarkItemDto[] _source = null!;

    /// <summary>Capacity hint; actual items added are RowCount × 2.</summary>
    [Params(1, 100, 1_000, 10_000, 100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        var doubleCount = RowCount * 2;
        _source = new BenchmarkItemDto[doubleCount];
        for (var i = 0; i < doubleCount; i++)
            _source[i] = new BenchmarkItemDto(
                rng.Next(1, 1_000_000),
                Guid.NewGuid(),
                $"item_{i:D6}",
                Math.Round((decimal)(rng.NextDouble() * 9999.99), 2),
                i % 2 == 0);
    }

    /// <summary>
    ///     Capacity=RowCount, adds RowCount×2 items — triggers exactly one internal-array doubling.
    ///     The extra memcopy shows up clearly at RowCount ≥ 10 000.
    /// </summary>
    [Benchmark(Baseline = true, Description = "new List<T>(RowCount) + AddRange(2×RowCount) — one forced resize")]
    public List<BenchmarkItemDto> Create_OverextendCapacity()
    {
        var list = new List<BenchmarkItemDto>(RowCount);
        list.AddRange(_source);
        return list;
    }
}
