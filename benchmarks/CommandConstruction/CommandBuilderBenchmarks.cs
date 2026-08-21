using CaeriusNet.Benchmark.Infrastructure;

namespace CaeriusNet.Benchmark.CommandConstruction;

[MemoryDiagnoser]
[BenchmarkCategory("command", "parameters")]
public class CommandBuilderBenchmarks : IDisposable
{
    private DateTime _asOf;
    private StoredProcedureCommand _cachedCommand = null!;
    private SqlConnection _connection = null!;
    private int _customerId;
    private BenchmarkTvpRow[] _tvpRows = [];

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [GlobalSetup]
    public void Setup()
    {
        _tvpRows =
        [
            new BenchmarkTvpRow(1, "one", 1.25m, DateTime.UnixEpoch),
            new BenchmarkTvpRow(2, "two", 2.50m, DateTime.UnixEpoch.AddMinutes(1))
        ];
        _customerId = 42;
        _asOf = DateTime.UnixEpoch.AddDays(20_000);
        // The provider does not need an open connection to allocate and populate a SqlCommand. Keeping this
        // disconnected makes this a repeatable binding benchmark, rather than an accidental network benchmark.
        _connection = new SqlConnection();

        _cachedCommand = CreateBuilder()
            .AddReturnValue()
            .AddParameter("@customerId", _customerId, SqlDbType.Int)
            .AddParameter("@asOf", _asOf, SqlDbType.DateTime2, scale: 7)
            .AddInputOutputParameter("@version", 3L, SqlDbType.BigInt)
            .AddOutputParameter("@status", SqlDbType.NVarChar, 64)
            .Build();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Dispose();
    }

    /// <summary>Builds the immutable command contract used by the normal scalar/output execution path.</summary>
    [Benchmark]
    public StoredProcedureCommand BuildScalarCommand()
    {
        return CreateBuilder()
            .AddReturnValue()
            .AddParameter("@customerId", _customerId, SqlDbType.Int)
            .AddParameter("@asOf", _asOf, SqlDbType.DateTime2, scale: 7)
            .AddInputOutputParameter("@version", 3L, SqlDbType.BigInt)
            .AddOutputParameter("@status", SqlDbType.NVarChar, 64)
            .Build();
    }

    [Benchmark]
    public StoredProcedureCommand BuildTvpCommand()
    {
        return CreateBuilder()
            .AddParameter("@customerId", _customerId, SqlDbType.Int)
            .AddTvpParameter("@items", _tvpRows)
            .Build();
    }

    /// <summary>
    ///     Measures the runtime's descriptor-to-<see cref="SqlCommand" /> binding, including all scalar, output,
    ///     input/output and return parameters, without opening a SQL Server connection.
    /// </summary>
    [Benchmark]
    public int BindScalarCommandIntoSqlCommand()
    {
        using var command = _cachedCommand.CreateSqlCommand(_connection);
        return command.Parameters.Count + command.CommandText.Length;
    }

    [Benchmark]
    public string BuildCanonicalCacheKey()
    {
        return CaeriusCacheKeyBuilder.Create("caerius-benchmark", "primary", _cachedCommand, "immutable-array");
    }

    private static StoredProcedureCommandBuilder CreateBuilder()
    {
        return new StoredProcedureCommandBuilder("dbo", "BenchmarkCommand")
            .WithResultSetCapacities(32)
            .WithContractHash("benchmark-contract");
    }
}
