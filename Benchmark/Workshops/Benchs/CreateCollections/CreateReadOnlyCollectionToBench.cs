using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections;

/// <summary>
///     Measures the cost of building a <see cref="ReadOnlyCollection{T}" /> from a pre-existing
///     source array across five row-count scales (1 → 100 000).
/// </summary>
/// <remarks>
///     <see cref="ReadOnlyCollection{T}" /> wraps an <see cref="IList{T}" /> by reference — no deep
///     copy of the elements occurs during the wrap itself.  The dominant cost is therefore the
///     <c>new List → AddRange → AsReadOnly()</c> pipeline, not the wrapper object overhead.
///     The LINQ variant adds an extra iteration step before the wrap.
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class CreateReadOnlyCollectionToBench
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

    /// <summary>
    ///     Canonical pipeline: pre-allocated List → AddRange → AsReadOnly.
    ///     One allocation for the backing array, one for the ReadOnlyCollection header.
    /// </summary>
    [Benchmark(Baseline = true, Description = "new List<T>(N) + AddRange + AsReadOnly() — exact capacity")]
    public ReadOnlyCollection<BenchmarkItemDto> Create_WithCapacity_AsReadOnly()
    {
        var list = new List<BenchmarkItemDto>(RowCount);
        list.AddRange(_source);
        return list.AsReadOnly();
    }

    /// <summary>LINQ ToList + wrap — convenience pattern that omits the capacity hint.</summary>
    [Benchmark(Description = "source.ToList() + AsReadOnly()")]
    public ReadOnlyCollection<BenchmarkItemDto> Create_LinqToList_AsReadOnly()
    {
        return _source.ToList().AsReadOnly();
    }
}