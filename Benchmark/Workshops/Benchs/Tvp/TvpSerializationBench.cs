using BenchmarkDotNet.Attributes;
using Bogus;
using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.Tvp;

/// <summary>
///     Benchmarks TVP (Table-Valued Parameter) serialization performance.
///     Measures how fast ITvpMapper.MapAsSqlDataRecords() can convert a List&lt;T&gt;
///     into IEnumerable&lt;SqlDataRecord&gt; for streaming to SQL Server.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class TvpSerializationBench
{
    private static readonly Faker<BenchmarkTvpItem> Faker = new Faker<BenchmarkTvpItem>()
        .CustomInstantiator(f => new BenchmarkTvpItem(
            f.Random.Int(1, 100_000),
            f.Internet.UserName(),
            Math.Round((decimal)f.Random.Double(0.01, 9999.99), 2)));

    private List<BenchmarkTvpItem> _items = null!;

    [Params(10, 100, 1_000, 10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _items = Faker.Generate(RowCount);
    }

    /// <summary>
    ///     Materialises the full SqlDataRecord IEnumerable into a list
    ///     (mirrors what SqlClient does when reading the TVP).
    /// </summary>
    [Benchmark(Baseline = true, Description = "TVP serialization (enumerate all records)")]
    public int Serialize_And_Enumerate_All()
    {
        var firstItem = _items[0];
        var records = firstItem.MapAsSqlDataRecords(_items);
        var count = 0;
        foreach (var _ in records)
            count++;
        return count;
    }

    /// <summary>
    ///     Materialises to array (ToArray() call) — worst case allocation.
    /// </summary>
    [Benchmark(Description = "TVP serialization (materialize to array)")]
    public int Serialize_And_Materialize_ToArray()
    {
        var firstItem = _items[0];
        var records = firstItem.MapAsSqlDataRecords(_items).ToArray();
        return records.Length;
    }
}
