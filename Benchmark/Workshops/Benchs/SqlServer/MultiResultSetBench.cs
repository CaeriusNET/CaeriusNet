namespace CaeriusNet.Benchmark.Workshops.Benchs.SqlServer;

/// <summary>
///     Benchmarks multi-result-set SP (one roundtrip) vs N separate SP calls.
///     Demonstrates roundtrip overhead reduction via multi-result patterns.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class MultiResultSetBench
{
    [GlobalSetup]
    public async Task Setup()
    {
        await SqlBenchmarkGlobalSetup.InitialiseAsync();
    }

    [Benchmark(Baseline = true, Description = "2 separate SP calls (2 roundtrips)")]
    public async Task<int> Two_Separate_SP_Calls()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return 0;

        await using var connection = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        await connection.OpenAsync();

        var total = 0;
        for (var call = 0; call < 2; call++)
        {
            await using var cmd = new SqlCommand("[dbo].[usp_GetBenchmarkItems]", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add(new SqlParameter("@Count", SqlDbType.Int) { Value = 50 });
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) total++;
        }

        return total;
    }

    [Benchmark(Description = "1 inline multi-result query (1 roundtrip)")]
    public async Task<int> One_MultiResult_Query()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return 0;

        await using var connection = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        await connection.OpenAsync();

        // Inline 2 SPs in one batch to demonstrate 1-roundtrip multi-result advantage
        const string multiSql = """
                                EXEC [dbo].[usp_GetBenchmarkItems] @Count = 50;
                                EXEC [dbo].[usp_GetBenchmarkItems] @Count = 50;
                                """;

        await using var cmd = new SqlCommand(multiSql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        var total = 0;
        do
        {
            while (await reader.ReadAsync()) total++;
        } while (await reader.NextResultAsync());

        return total;
    }
}
