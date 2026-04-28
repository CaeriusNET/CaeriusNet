using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.SqlServer;

/// <summary>
///     Compares N individual INSERT calls (single row SP) vs one TVP batch INSERT.
///     This is the core value-proposition benchmark of CaeriusNet: batch via TVP is dramatically faster.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class BatchedVsSingleBench
{
    private static readonly Faker<BenchmarkTvpItem> Faker = new Faker<BenchmarkTvpItem>()
        .CustomInstantiator(f => new BenchmarkTvpItem(
            f.Random.Int(1, 100_000),
            f.Internet.UserName(),
            Math.Round((decimal)f.Random.Double(0.01, 9999.99), 2)));

    private List<BenchmarkTvpItem> _items = null!;

    [Params(10, 100, 500, 1_000, 5_000)] public int ItemCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        await SqlBenchmarkGlobalSetup.InitialiseAsync();
        _items = Faker.Generate(ItemCount);
    }

    [Benchmark(Baseline = true, Description = "N single-row SP calls (loop)")]
    public async Task Insert_N_SingleRow_StoredProcedureCalls()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return;

        await using var connection = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        await connection.OpenAsync();

        foreach (var item in _items)
        {
            await using var cmd = new SqlCommand("[dbo].[usp_InsertBenchmarkItem]", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = item.Name });
            cmd.Parameters.Add(new SqlParameter("@Price", SqlDbType.Decimal) { Value = item.Price });
            await cmd.ExecuteNonQueryAsync();
        }
    }

    [Benchmark(Description = "1 TVP batch SP call")]
    public async Task Insert_Batched_Via_TVP_StoredProcedureCall()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return;

        await using var connection = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        await connection.OpenAsync();

        var firstItem = _items[0];
        var records = firstItem.MapAsSqlDataRecords(_items);

        await using var cmd = new SqlCommand("[dbo].[usp_InsertBenchmarkItemsBatch]", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        var tvpParam = new SqlParameter("@Items", SqlDbType.Structured)
        {
            TypeName = "dbo.tvp_BenchmarkItem",
            Value = records
        };
        cmd.Parameters.Add(tvpParam);
        await cmd.ExecuteNonQueryAsync();
    }
}
