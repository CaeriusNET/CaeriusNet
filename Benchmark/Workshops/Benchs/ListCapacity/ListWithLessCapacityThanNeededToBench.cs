using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ListCapacity;

/// <summary>
///     Measures <see cref="List{T}" /> construction cost when the capacity hint is exactly half the
///     number of items that will be added — systematic under-allocation by a factor of two.
/// </summary>
/// <remarks>
///     Setting <c>capacity = RowCount / 2</c> while adding <c>RowCount</c> elements forces the list
///     to resize once: the initial half-sized array is copied into a new array of at least
///     <c>RowCount</c> slots.  The Ratio column vs <see cref="ListWithCapacityToBench"/> isolates
///     the cost of that single unnecessary copy at each scale.  At 100 000 rows the difference
///     becomes non-trivial and visible in both the Mean and Allocated columns.
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ListWithLessCapacityThanNeededToBench
{
    private BenchmarkItemDto[] _source = null!;

    /// <summary>Actual item count; capacity hint is RowCount / 2.</summary>
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
    ///     Under-capacity: hint = max(1, RowCount / 2), add RowCount items.
    ///     Forces one resize; the initial half-sized backing array is abandoned to GC.
    /// </summary>
    [Benchmark(Baseline = true, Description = "new List<T>(RowCount/2) + AddRange(RowCount) — one forced resize")]
    public List<BenchmarkItemDto> Create_UnderCapacity()
    {
        var hint = Math.Max(1, RowCount / 2);
        var list = new List<BenchmarkItemDto>(hint);
        list.AddRange(_source);
        return list;
    }
}
