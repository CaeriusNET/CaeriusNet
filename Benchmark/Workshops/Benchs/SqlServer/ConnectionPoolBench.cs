namespace CaeriusNet.Benchmark.Workshops.Benchs.SqlServer;

/// <summary>
///     Benchmarks SQL Server connection acquisition and re-use patterns.
/// </summary>
/// <remarks>
///     <para>
///         ADO.NET's built-in connection pooler (SSCP) maintains a pool of pre-opened TCP connections
///         to SQL Server. Re-using a pooled connection is near-instant (&lt;1 ms); a <em>cold</em> connection
///         (cleared pool or first open) requires a full TDS handshake (3–15 ms depending on network).
///     </para>
///     <para>
///         This benchmark measures:
///         <list type="number">
///             <item><b>Warm pool</b> — <c>OpenAsync</c> on a connection whose pool slot is warm.</item>
///             <item>
///                 <b>Pool cold-clear</b> — <c>SqlConnection.ClearPool</c> before <c>OpenAsync</c>; forces new TDS
///                 handshake.
///             </item>
///             <item><b>Reuse single connection</b> — baseline: no <c>OpenAsync</c> overhead at all.</item>
///         </list>
///         Note: <c>ClearPool</c> benches are intentionally slow. Their purpose is to show the cost
///         you are <em>avoiding</em> by using the pool, not to optimise.
///     </para>
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public sealed class ConnectionPoolBench : IDisposable
{
    private SqlConnection? _persistentConnection;

    public void Dispose()
    {
        _persistentConnection?.Dispose();
        GC.SuppressFinalize(this);
    }

    [GlobalSetup]
    public async Task Setup()
    {
        await SqlBenchmarkGlobalSetup.InitialiseAsync();

        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return;

        // Prime the connection pool by opening and closing a connection once
        await using var warm = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        await warm.OpenAsync();

        // Persistent connection for the "reuse" bench
        _persistentConnection = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        await _persistentConnection.OpenAsync();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_persistentConnection is not null)
        {
            await _persistentConnection.DisposeAsync();
            _persistentConnection = null;
        }
    }

    /// <summary>
    ///     Baseline: reuse a single persistent <see cref="SqlConnection" />.
    ///     No <c>OpenAsync</c> / <c>CloseAsync</c> overhead — pure SP execution cost.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Reuse single open connection (no open/close overhead)")]
    public async Task<int> ExecuteSP_ReuseConnection()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable || _persistentConnection is null) return -1;

        await using var cmd = new SqlCommand("[dbo].[usp_GetBenchmarkItems]", _persistentConnection)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add(new SqlParameter("@Count", SqlDbType.Int) { Value = 10 });

        var count = 0;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            count++;
        return count;
    }

    /// <summary>
    ///     Typical ADO.NET pattern: create, open (pool warm), execute, dispose.
    ///     <c>OpenAsync</c> is expected to be &lt;1 ms (pool lookup, no TCP handshake).
    /// </summary>
    [Benchmark(Description = "Pooled connection: new SqlConnection + OpenAsync (pool warm)")]
    public async Task<int> ExecuteSP_WarmPooledConnection()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return -1;

        await using var connection = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = new SqlCommand("[dbo].[usp_GetBenchmarkItems]", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add(new SqlParameter("@Count", SqlDbType.Int) { Value = 10 });

        var count = 0;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            count++;
        return count;
    }

    /// <summary>
    ///     Cold-start: <c>SqlConnection.ClearPool</c> forces a new TDS handshake on next open.
    ///     Shows the worst-case connection cost (e.g., k8s pod start, pool timeout).
    ///     Expected to be 10–50× slower than the warm-pool bench.
    /// </summary>
    [Benchmark(Description = "Cold-start connection: ClearPool + OpenAsync (new TDS handshake)")]
    public async Task<int> ExecuteSP_ColdStartConnection()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return -1;

        await using var connection = new SqlConnection(SqlBenchmarkGlobalSetup.ConnectionString);
        SqlConnection.ClearPool(connection); // Forces a full cold TDS handshake on next OpenAsync
        await connection.OpenAsync();

        await using var cmd = new SqlCommand("[dbo].[usp_GetBenchmarkItems]", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add(new SqlParameter("@Count", SqlDbType.Int) { Value = 10 });

        var count = 0;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            count++;
        return count;
    }
}
