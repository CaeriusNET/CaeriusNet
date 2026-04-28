using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections;

/// <summary>
///     Measures the cost of constructing an <see cref="ImmutableArray{T}" /> from a pre-existing
///     source array across five row-count scales (1 → 100 000).
/// </summary>
/// <remarks>
///     Two construction paths are compared:
///     <list type="bullet">
///         <item>
///             <term>CreateBuilder (baseline)</term>
///             <description>
///                 Pre-allocates a typed builder, fills it via <c>AddRange</c>, then seals it with
///                 <c>MoveToImmutable()</c>.  When the capacity matches exactly, MoveToImmutable performs
///                 a zero-copy move of the internal array — no second allocation occurs.
///             </description>
///         </item>
///         <item>
///             <term>ImmutableArray.Create(Span)</term>
///             <description>
///                 Constructs the immutable array directly from a <see cref="ReadOnlySpan{T}" />:
///                 a single internal memcopy with no builder overhead.  Fastest path for array-sourced data.
///             </description>
///         </item>
///     </list>
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class CreateImmutableArrayToBench
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
    ///     Builder path: pre-allocated capacity → AddRange → MoveToImmutable.
    ///     Zero-copy move when capacity is exact — one internal array allocation in total.
    /// </summary>
    [Benchmark(Baseline = true, Description = "CreateBuilder(N) + AddRange + MoveToImmutable()")]
    public ImmutableArray<BenchmarkItemDto> Create_Builder_MoveToImmutable()
    {
        var builder = ImmutableArray.CreateBuilder<BenchmarkItemDto>(RowCount);
        builder.AddRange(_source);
        return builder.MoveToImmutable();
    }

    /// <summary>
    ///     Direct span construction: single internal memcopy from source span — no builder overhead.
    ///     Fastest path for array-sourced data; preferred when source is already an array or Span.
    /// </summary>
    [Benchmark(Description = "ImmutableArray.Create(ReadOnlySpan<T>) — single memcopy")]
    public ImmutableArray<BenchmarkItemDto> Create_FromSpan()
    {
        return ImmutableArray.Create<BenchmarkItemDto>(_source.AsSpan());
    }
}
