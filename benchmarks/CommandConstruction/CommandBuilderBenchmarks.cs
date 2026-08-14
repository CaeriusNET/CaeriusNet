using CaeriusNet.Benchmark.Infrastructure;

namespace CaeriusNet.Benchmark.CommandConstruction;

[MemoryDiagnoser]
[WarmupCount(3)]
[IterationCount(10)]
[BenchmarkCategory("command", "parameters")]
public class CommandBuilderBenchmarks
{
    private StoredProcedureCommand _cachedCommand = null!;
    private int _customerId;
    private BenchmarkTvpRow[] _tvpRows = [];

    [GlobalSetup]
    public void Setup()
    {
        _tvpRows =
        [
            new BenchmarkTvpRow(1, "one", 1.25m, DateTime.UnixEpoch),
            new BenchmarkTvpRow(2, "two", 2.50m, DateTime.UnixEpoch.AddMinutes(1))
        ];
        _customerId = 42;

        _cachedCommand = CreateBuilder()
            .AddParameter("@customerId", _customerId, SqlDbType.Int)
            .AddParameter("@asOf", DateTime.UtcNow, SqlDbType.DateTime2, scale: 7)
            .AddInputOutputParameter("@version", 3L, SqlDbType.BigInt)
            .AddOutputParameter("@status", SqlDbType.NVarChar, 64)
            .AddReturnValue()
            .Build();
    }

    [Benchmark(Baseline = true)]
    public StoredProcedureCommand BuildScalarCommand()
    {
        return CreateBuilder()
            .AddParameter("@customerId", _customerId, SqlDbType.Int)
            .AddParameter("@asOf", DateTime.UtcNow, SqlDbType.DateTime2, scale: 7)
            .AddInputOutputParameter("@version", 3L, SqlDbType.BigInt)
            .AddOutputParameter("@status", SqlDbType.NVarChar, 64)
            .AddReturnValue()
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

    [Benchmark]
    public string BuildCanonicalCacheKey()
    {
        return CaeriusCacheKeyBuilder.Create("caerius-benchmark", "primary", _cachedCommand, "immutable-array");
    }

    private static StoredProcedureCommandBuilder CreateBuilder()
    {
        return new StoredProcedureCommandBuilder("dbo", "BenchmarkCommand", 30)
            .WithResultSetCapacities(32)
            .WithContractHash("benchmark-contract");
    }
}
