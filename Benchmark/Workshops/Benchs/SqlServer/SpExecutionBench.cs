namespace CaeriusNet.Benchmark.Workshops.Benchs.SqlServer;

/// <summary>
///     Benchmarks the full roundtrip time of executing a stored procedure against SQL Server
///     and materialising the result set into a List.
///     Skipped automatically when BENCHMARK_SQL_CONNECTION is not set.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class SpExecutionBench
{
    [Params(0, 10, 100, 1_000, 5_000, 10_000, 50_000)] public int RowCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        await SqlBenchmarkGlobalSetup.InitialiseAsync();
    }

    [Benchmark(Description = "SP roundtrip: SELECT TOP @Count → List<T>")]
    public async Task<int> Execute_StoredProcedure_And_Materialise()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return 0;

        await using var connection = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = new SqlCommand("[dbo].[usp_GetBenchmarkItems]", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add(new SqlParameter("@Count", SqlDbType.Int) { Value = RowCount });

        var count = 0;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            count++;

        return count;
    }
}