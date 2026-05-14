using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

/// <summary>
///     Measures sequential-read throughput of <see cref="ImmutableArray{T}" /> across five
///     row-count scales (1 → 100 000).
/// </summary>
/// <remarks>
///     <see cref="ImmutableArray{T}" /> stores elements in a contiguous array, enabling the JIT to
///     generate optimal vector-load instructions.  Its enumerator is a value-type (<c>struct</c>),
///     so BDN's foreach path avoids boxing and virtual dispatch entirely.
///     Comparing this class against <see cref="ReadListToBench" /> reveals whether the immutability
///     guarantee and contiguous layout actually yield measurable throughput improvements.
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ReadImmutableArrayToBench
{
    private ImmutableArray<BenchmarkItemDto> _data;

    /// <summary>Number of elements in the immutable array under test.</summary>
    [Params(1, 100, 1_000, 10_000, 100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        var builder = ImmutableArray.CreateBuilder<BenchmarkItemDto>(RowCount);
        for (var i = 0; i < RowCount; i++)
            builder.Add(new BenchmarkItemDto(
                rng.Next(1, 1_000_000),
                Guid.NewGuid(),
                $"item_{i:D6}",
                Math.Round((decimal)(rng.NextDouble() * 9999.99), 2),
                i % 2 == 0));
        _data = builder.MoveToImmutable();
    }

    /// <summary>Struct enumerator — zero boxing, no virtual dispatch, JIT-vectorisable inner loop.</summary>
    [Benchmark(Baseline = true, Description = "foreach — struct enumerator, accumulate Sum(Id)")]
    public int Read_ForEach()
    {
        var sum = 0;
        foreach (var item in _data)
            sum += item.Id;
        return sum;
    }

    /// <summary>LINQ Sum — forces IEnumerable interface; loses the struct-enumerator advantage.</summary>
    [Benchmark(Description = "LINQ .Sum(item => item.Id)")]
    public int Read_LinqSum()
    {
        return _data.Sum(item => item.Id);
    }
}