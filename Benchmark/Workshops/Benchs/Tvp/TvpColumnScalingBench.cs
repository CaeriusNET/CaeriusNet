using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.Tvp;

/// <summary>
///     Measures how TVP serialisation cost scales with the number of columns.
/// </summary>
/// <remarks>
///     <para>
///         The source-generated <c>MapAsSqlDataRecords()</c> calls one <c>record.SetXxx(ordinal, value)</c>
///         per column per row. This benchmark isolates the column-count factor from the row-count factor,
///         helping quantify the per-column cost of the generated SetXxx calls.
///     </para>
///     <para>
///         The <c>SqlMetaData[]</c> schema array is <c>private static readonly</c> — allocated once at
///         type-load time. Only the single reused <see cref="SqlDataRecord" /> is allocated per benchmark run.
///     </para>
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.CacheMisses)]
public class TvpColumnScalingBench
{
    private static readonly Faker<BenchmarkTvpItem> Faker3 = new Faker<BenchmarkTvpItem>()
        .CustomInstantiator(f => new BenchmarkTvpItem(
            f.Random.Int(1, 100_000), f.Internet.UserName(),
            Math.Round((decimal)f.Random.Double(0.01, 9999.99), 2)));

    private static readonly Faker<BenchmarkTvpItem5Col> Faker5 = new Faker<BenchmarkTvpItem5Col>()
        .CustomInstantiator(f => new BenchmarkTvpItem5Col(
            f.Random.Int(1, 100_000), f.Internet.UserName(),
            Math.Round((decimal)f.Random.Double(0.01, 9999.99), 2),
            f.Random.Bool(), f.Date.Recent()));

    private static readonly Faker<BenchmarkTvpItem10Col> Faker10 = new Faker<BenchmarkTvpItem10Col>()
        .CustomInstantiator(f => new BenchmarkTvpItem10Col(
            f.Random.Int(1, 100_000), f.Internet.UserName(),
            Math.Round((decimal)f.Random.Double(0.01, 9999.99), 2),
            f.Random.Bool(), f.Date.Recent(),
            f.Commerce.Department(), f.Random.Int(1, 500),
            Math.Round((decimal)f.Random.Double(0.0, 100.0), 4),
            f.Lorem.Sentence(), f.Random.Guid()));

    private List<BenchmarkTvpItem10Col> _items10 = null!;

    private List<BenchmarkTvpItem> _items3 = null!;
    private List<BenchmarkTvpItem5Col> _items5 = null!;

    [Params(10, 100, 1_000, 10_000, 50_000, 100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        Randomizer.Seed = new Random(42);
        _items3 = Faker3.Generate(RowCount);
        _items5 = Faker5.Generate(RowCount);
        _items10 = Faker10.Generate(RowCount);
    }

    /// <summary>3-column TVP: Id (int), Name (nvarchar), Price (decimal).</summary>
    [Benchmark(Baseline = true, Description = "3-col TVP: int + nvarchar + decimal")]
    public int Tvp_3_Columns()
    {
        var count = 0;
        foreach (var _ in _items3[0].MapAsSqlDataRecords(_items3))
            count++;
        return count;
    }

    /// <summary>5-column TVP: adds bool + DateTime to the 3-col schema.</summary>
    [Benchmark(Description = "5-col TVP: + bool + datetime2")]
    public int Tvp_5_Columns()
    {
        var count = 0;
        foreach (var _ in _items5[0].MapAsSqlDataRecords(_items5))
            count++;
        return count;
    }

    /// <summary>
    ///     10-column TVP: full mixed-type schema (int, nvarchar, decimal, bool, datetime2, nvarchar, int, decimal,
    ///     nvarchar, uniqueidentifier).
    /// </summary>
    [Benchmark(Description = "10-col TVP: full mixed-type schema")]
    public int Tvp_10_Columns()
    {
        var count = 0;
        foreach (var _ in _items10[0].MapAsSqlDataRecords(_items10))
            count++;
        return count;
    }
}