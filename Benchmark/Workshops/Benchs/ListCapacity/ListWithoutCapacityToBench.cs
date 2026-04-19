using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ListCapacity;

/// <summary>
///     Measures <see cref="List{T}" /> construction cost with no capacity hint (default constructor).
/// </summary>
/// <remarks>
///     Without a hint, <see cref="List{T}" /> starts with capacity 0 and doubles on each overflow:
///     0 → 4 → 8 → 16 → … → N.  For large N this triggers O(log₂ N) reallocations, each of which
///     copies the existing elements into a new, larger array.  The overhead is visible both in the
///     Mean column (extra copy work) and the Gen0/Allocated columns (wasted intermediate arrays).
///     Compare against <see cref="ListWithCapacityToBench" /> to quantify the exact cost of omitting
///     the capacity hint at each scale.
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ListWithoutCapacityToBench
{
    private BenchmarkItemDto[] _source = null!;

    /// <summary>Number of items to add; no capacity hint is provided to the List constructor.</summary>
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

    /// <summary>
    ///     No capacity hint — internal array starts at 0 and doubles on every overflow.
    ///     At RowCount=100 000 this triggers ~17 doubling steps, wasting ~50 % of the final array.
    /// </summary>
    [Benchmark(Baseline = true, Description = "new List<T>() + AddRange — no hint, O(log N) doublings")]
    public List<BenchmarkItemDto> Create_NoCapacity()
    {
        var list = new List<BenchmarkItemDto>();
        list.AddRange(_source);
        return list;
    }
}