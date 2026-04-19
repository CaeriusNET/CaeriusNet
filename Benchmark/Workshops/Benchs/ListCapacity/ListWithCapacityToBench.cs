using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ListCapacity;

/// <summary>
///     Measures <see cref="List{T}" /> construction cost when the capacity hint equals exactly the
///     number of items to be added — the theoretical minimum allocation scenario.
/// </summary>
/// <remarks>
///     Setting <c>new List&lt;T&gt;(RowCount)</c> allocates one internal array of exactly the
///     required size.  <c>AddRange</c> then performs a single <c>Array.Copy</c> with zero resize.
///     This benchmark establishes the floor allocation cost for list construction and acts as the
///     canonical baseline when comparing under- and over-allocation strategies.
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ListWithCapacityToBench
{
    private BenchmarkItemDto[] _source = null!;

    /// <summary>Number of items to add; capacity is set to this exact value.</summary>
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
    ///     Exact capacity: one array allocation, one Array.Copy — zero resize overhead.
    ///     Optimal pattern when row count is known ahead of the loop (e.g. from SqlDataReader.FieldCount
    ///     or a COUNT query).
    /// </summary>
    [Benchmark(Baseline = true, Description = "new List<T>(exact capacity) + AddRange")]
    public List<BenchmarkItemDto> Create_ExactCapacity()
    {
        var list = new List<BenchmarkItemDto>(RowCount);
        list.AddRange(_source);
        return list;
    }
}