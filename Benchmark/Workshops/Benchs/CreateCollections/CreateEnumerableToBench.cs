using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections;

/// <summary>
///     Measures the cost of exposing a collection as <see cref="IEnumerable{T}" /> from a source
///     array across five row-count scales (1 → 100 000).
/// </summary>
/// <remarks>
///     Two paths are contrasted:
///     <list type="bullet">
///         <item>
///             <term>Materialise then wrap (baseline)</term>
///             <description>
///                 Fills a pre-allocated <see cref="List{T}" /> and wraps it with
///                 <see cref="Enumerable.AsEnumerable{TSource}" /> — a zero-allocation identity cast.
///                 Dominant cost is the List construction itself.
///             </description>
///         </item>
///         <item>
///             <term>Zero-copy array wrap</term>
///             <description>
///                 Calls <c>AsEnumerable()</c> directly on the source array — no intermediate List is
///                 created.  Near-zero allocation at all scales; demonstrates why skipping materialisation
///                 matters for read-only enumeration paths.
///             </description>
///         </item>
///     </list>
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class CreateEnumerableToBench
{
    private BenchmarkItemDto[] _source = null!;

    /// <summary>Number of items in the source array per benchmark call.</summary>
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
    ///     Materialise into a List then wrap: new List(N) + AddRange + AsEnumerable().
    ///     Allocates a full backing list before the costless identity cast.
    /// </summary>
    [Benchmark(Baseline = true, Description = "new List<T>(N) + AddRange + AsEnumerable()")]
    public IEnumerable<BenchmarkItemDto> Create_WithCapacity_AsEnumerable()
    {
        var list = new List<BenchmarkItemDto>(RowCount);
        list.AddRange(_source);
        return list.AsEnumerable();
    }

    /// <summary>Zero-copy: wrap the source array directly as IEnumerable — no List allocated.</summary>
    [Benchmark(Description = "array.AsEnumerable() — zero-copy lazy wrap")]
    public IEnumerable<BenchmarkItemDto> Create_Array_AsEnumerable()
    {
        return _source.AsEnumerable();
    }
}