namespace CaeriusNet.Benchmark.Workshops.Benchs.SqlServer;

/// <summary>
///     Benchmarks the <c>@NewId INT OUTPUT</c> stored procedure pattern.
/// </summary>
/// <remarks>
///     <para>
///         OUTPUT parameters are a common SQL Server pattern for retrieving identity values after an insert.
///         This benchmark compares three approaches:
///         <list type="number">
///             <item>
///                 CaeriusNet builder with explicit OUTPUT parameter via
///                 <see cref="StoredProcedureParametersBuilder.AddParameter" />
///             </item>
///             <item>Manual <see cref="SqlParameter" /> with <see cref="System.Data.ParameterDirection.Output" /></item>
///             <item>Manual SP call + immediate separate <c>SELECT SCOPE_IDENTITY()</c> (legacy anti-pattern)</item>
///         </list>
///     </para>
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class SpOutputParameterBench
{
    [GlobalSetup]
    public async Task Setup()
    {
        await SqlBenchmarkGlobalSetup.InitialiseAsync();
    }

    /// <summary>
    ///     CaeriusNet builder: constructs the OUTPUT parameter via <c>AddParameter</c> with
    ///     <c>ParameterDirection.Output</c> applied after build (workaround since builder targets input params).
    ///     Full roundtrip: build → execute → read OUTPUT.
    /// </summary>
    [Benchmark(Baseline = true, Description = "CaeriusNet: build SP → manual OUTPUT param patch → execute")]
    public async Task<int> CaeriusNet_SpWithOutput()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return -1;

        var spParams = new StoredProcedureParametersBuilder("dbo", "usp_InsertBenchmarkItemWithOutput")
            .AddParameter("@Name", "BenchItem", SqlDbType.NVarChar)
            .AddParameter("@Price", 9.99m, SqlDbType.Decimal)
            .Build();

        await using var connection = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = new SqlCommand($"[{spParams.SchemaName}].[{spParams.ProcedureName}]", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = spParams.CommandTimeout
        };
        cmd.Parameters.AddRange(spParams.GetParametersSpan().ToArray());

        // Add OUTPUT parameter separately (builder doesn't yet support ParameterDirection)
        var outParam = new SqlParameter("@NewId", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(outParam);

        await cmd.ExecuteNonQueryAsync();
        return (int)outParam.Value;
    }

    /// <summary>
    ///     Fully manual approach: direct <see cref="SqlCommand" /> with OUTPUT parameter,
    ///     no builder overhead. Establishes the minimum cost baseline.
    /// </summary>
    [Benchmark(Description = "Manual: SqlCommand + OUTPUT SqlParameter (direct, no builder)")]
    public async Task<int> Manual_SpWithOutput()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return -1;

        await using var connection = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = new SqlCommand("[dbo].[usp_InsertBenchmarkItemWithOutput]", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar) { Value = "BenchItem" });
        cmd.Parameters.Add(new SqlParameter("@Price", SqlDbType.Decimal) { Value = 9.99m });
        cmd.Parameters.Add(new SqlParameter("@NewId", SqlDbType.Int)
            { Direction = ParameterDirection.Output });

        await cmd.ExecuteNonQueryAsync();
        return (int)cmd.Parameters["@NewId"].Value;
    }

    /// <summary>
    ///     Legacy anti-pattern: INSERT then immediately run a separate <c>SELECT SCOPE_IDENTITY()</c>.
    ///     Two round-trips vs one — demonstrates the cost of not using OUTPUT parameters.
    /// </summary>
    [Benchmark(Description = "Legacy: INSERT SP + separate SELECT SCOPE_IDENTITY() (2 round-trips)")]
    public async Task<int> Legacy_ScopeIdentity_TwoRoundtrips()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return -1;

        await using var connection = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        await connection.OpenAsync();

        // Round-trip 1: Execute the SP without OUTPUT param
        await using var insertCmd = new SqlCommand("[dbo].[usp_InsertBenchmarkItem]", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        insertCmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar) { Value = "BenchItem" });
        insertCmd.Parameters.Add(new SqlParameter("@Price", SqlDbType.Decimal) { Value = 9.99m });
        await insertCmd.ExecuteNonQueryAsync();

        // Round-trip 2: Retrieve the identity separately
        await using var idCmd = new SqlCommand("SELECT CAST(SCOPE_IDENTITY() AS INT);", connection);
        var result = await idCmd.ExecuteScalarAsync();
        return result is DBNull or null ? -1 : (int)result;
    }
}
