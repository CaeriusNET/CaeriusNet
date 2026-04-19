using BenchmarkDotNet.Attributes;
using Bogus;
using CaeriusNet.Benchmark.Data.Generated;
using CaeriusNet.Benchmark.Workshops.Benchs.SqlServer;
using CaeriusNet.Builders;
using Microsoft.Data.SqlClient;

namespace CaeriusNet.Benchmark.Workshops.Benchs.SqlServer;

/// <summary>
///     Benchmarks the full TVP lifecycle end-to-end against SQL Server.
/// </summary>
/// <remarks>
///     <para>
///         This benchmark exercises the complete pipeline:
///         <list type="number">
///             <item>Generate in-memory items with Bogus</item>
///             <item>Wrap them in <see cref="StoredProcedureParametersBuilder.AddTvpParameter{T}" /></item>
///             <item>Call <see cref="StoredProcedureParametersBuilder.Build" /></item>
///             <item>Open a pooled <see cref="SqlConnection" /> and execute the SP via <see cref="SqlCommand" /></item>
///             <item>Stream back the <c>OUTPUT INSERTED.*</c> rows from the <see cref="SqlDataReader" /></item>
///         </list>
///         Comparing this with the manual (no-builder) baseline measures the actual overhead introduced
///         by CaeriusNet's builder and TVP mapper.
///     </para>
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class TvpFullRoundtripBench
{
    private static readonly Faker<BenchmarkTvpItem5Col> Faker = new Faker<BenchmarkTvpItem5Col>()
        .CustomInstantiator(f => new BenchmarkTvpItem5Col(
            f.Random.Int(1, 100_000),
            f.Internet.UserName(),
            Math.Round((decimal)f.Random.Double(0.01, 9999.99), 4),
            f.Random.Bool(),
            f.Date.Recent()));

    private List<BenchmarkTvpItem5Col> _items = null!;

    [Params(10, 100, 1_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        Randomizer.Seed = new Random(42);
        await SqlBenchmarkGlobalSetup.InitialiseAsync();
        _items = Faker.Generate(RowCount);
    }

    /// <summary>
    ///     CaeriusNet path: builder + <c>AddTvpParameter&lt;T&gt;</c> + <c>Build()</c> + execute + stream OUTPUT.
    ///     Measures the full end-to-end latency including the source-generated mapper overhead.
    /// </summary>
    [Benchmark(Baseline = true, Description = "CaeriusNet: builder → AddTvpParameter → Build → execute → stream OUTPUT")]
    public async Task<int> CaeriusNet_TvpFullRoundtrip()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return -1;

        var spParams = new StoredProcedureParametersBuilder("dbo", "usp_InsertBenchmarkItemsBatch_WithOutput",
                ResultSetCapacity: RowCount)
            .AddTvpParameter("@Items", _items)
            .Build();

        var count = 0;
        await using var connection = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = new SqlCommand($"[{spParams.SchemaName}].[{spParams.ProcedureName}]", connection)
        {
            CommandType = System.Data.CommandType.StoredProcedure,
            CommandTimeout = spParams.CommandTimeout
        };
        cmd.Parameters.AddRange(spParams.GetParametersSpan().ToArray());

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            count++;

        return count;
    }

    /// <summary>
    ///     Manual path: direct <see cref="SqlParameter" /> + <see cref="Microsoft.Data.SqlClient.Server.SqlDataRecord" /> without the builder.
    ///     Establishes the minimum achievable cost for this operation.
    /// </summary>
    [Benchmark(Description = "Manual: direct SqlParameter(Structured) → execute → stream OUTPUT")]
    public async Task<int> Manual_TvpFullRoundtrip()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return -1;

        var count = 0;
        await using var connection = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = new SqlCommand("[dbo].[usp_InsertBenchmarkItemsBatch_WithOutput]", connection)
        {
            CommandType = System.Data.CommandType.StoredProcedure
        };

        var firstItem = _items[0];
        var tvpParam = new SqlParameter("@Items", System.Data.SqlDbType.Structured)
        {
            TypeName = BenchmarkTvpItem5Col.TvpTypeName,
            Value = firstItem.MapAsSqlDataRecords(_items)
        };
        cmd.Parameters.Add(tvpParam);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            count++;

        return count;
    }
}
