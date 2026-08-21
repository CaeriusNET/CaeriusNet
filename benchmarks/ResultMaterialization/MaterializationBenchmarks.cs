using CaeriusNet.Benchmark.Infrastructure;

namespace CaeriusNet.Benchmark.ResultMaterialization;

[MemoryDiagnoser]
[BenchmarkCategory("mapping", "scalar", "collections")]
public class MaterializationBenchmarks
{
    private object _boxedDecimal = null!;
    private object _boxedInt = null!;
    private int _dtoId;
    private string _guidText = string.Empty;
    private BenchmarkDto[] _rows = [];

    /// <summary>Representative cardinalities for 1, 2, 5 and 10 result-set procedures.</summary>
    [Params(1, 2, 5, 10)]
    public int ResultSetCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _rows = Enumerable.Range(0, 64)
            .Select(static index => new BenchmarkDto(
                index,
                "row-" + index.ToString(CultureInfo.InvariantCulture),
                index / 10m,
                DateTimeOffset.UnixEpoch.AddMinutes(index),
                (index & 1) == 0,
                index % 5 == 0 ? null : "optional"))
            .ToArray();
        _boxedInt = 42;
        _boxedDecimal = 12345.6789m;
        _guidText = "2a9d49ff-2f91-4d2a-b69d-1aac1f8c83a1";
        _dtoId = 42;
    }

    /// <summary>Typed DTO construction is the in-memory half of row materialization.</summary>
    [Benchmark]
    public BenchmarkDto MaterializeDto()
    {
        return new BenchmarkDto(_dtoId, "meaning", 42.5m, DateTimeOffset.UnixEpoch, true, null);
    }

    [Benchmark]
    public int ConvertScalarInt32()
    {
        return Convert.ToInt32(_boxedInt, CultureInfo.InvariantCulture);
    }

    [Benchmark]
    public decimal ConvertScalarDecimal()
    {
        return Convert.ToDecimal(_boxedDecimal, CultureInfo.InvariantCulture);
    }

    [Benchmark]
    public Guid ParseScalarGuid()
    {
        return Guid.Parse(_guidText);
    }

    /// <summary>
    ///     <see cref="IEnumerable{T}" /> is deliberately already materialized: this mirrors the runtime API contract.
    /// </summary>
    [Benchmark(Baseline = true)]
    public object[] MaterializeEnumerableResultSets()
    {
        return Materialize(static source => source.ToArray());
    }

    [Benchmark]
    public object[] MaterializeReadOnlyCollectionResultSets()
    {
        return Materialize(static source => new ReadOnlyCollection<BenchmarkDto>(source.ToArray()));
    }

    [Benchmark]
    public object[] MaterializeImmutableArrayResultSets()
    {
        return Materialize(static source => source.ToImmutableArray());
    }

    private object[] Materialize(Func<BenchmarkDto[], object> create)
    {
        var resultSets = new object[ResultSetCount];
        for (var index = 0; index < resultSets.Length; index++)
            resultSets[index] = create(_rows);
        return resultSets;
    }
}
