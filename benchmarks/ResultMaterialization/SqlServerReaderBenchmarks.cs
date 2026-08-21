using CaeriusNet.Benchmark.Infrastructure;

namespace CaeriusNet.Benchmark.ResultMaterialization;

/// <summary>
///     Opt-in end-to-end SQL Server measurements for the ordinary reader and generated multi-result APIs. They are
///     intentionally outside the default <c>all</c> category: CI's portable gate has no database, whereas this
///     category needs a dedicated disposable SQL Server database supplied through the environment.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("sql-server", "reader", "mapping", "multi-result", "integration")]
public class SqlServerReaderBenchmarks
{
    private ICaeriusNetDbContext _database = null!;
    private StoredProcedureCommand _five = null!;
    private StoredProcedureCommand _one = null!;
    private IServiceScope _scope = null!;
    private ServiceProvider _services = null!;
    private StoredProcedureCommand _ten = null!;
    private StoredProcedureCommand _two = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        var connectionString = BenchmarkEnvironment.RequireSqlServerConnectionString();
        await BenchmarkSqlServerDatabase.ProvisionAsync(connectionString).ConfigureAwait(false);

        var services = new ServiceCollection();
        services.AddCaeriusNet(options => options.UseSqlServer(connectionString));
        _services = services.BuildServiceProvider();
        _scope = _services.CreateScope();
        _database = _scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        _one = CreateCommand(1);
        _two = CreateCommand(2);
        _five = CreateCommand(5);
        _ten = CreateCommand(10);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.Dispose();
        _services.Dispose();
    }

    /// <summary>Baseline: opens a real reader and maps 32 DTO rows through the generated ordinal mapper.</summary>
    [Benchmark(Baseline = true)]
    public async ValueTask<int> ReadOneResultSet()
    {
        var result = await _database.QueryImmutableArrayAsync<BenchmarkDto>(_one, 32).ConfigureAwait(false);
        return result.Length;
    }

    /// <summary>Exercises the generated two-result API, including static plan lookup and both reader transitions.</summary>
    [Benchmark]
    public async ValueTask<int> ReadTwoResultSets()
    {
        var result = await _database.QueryMultipleImmutableArrayAsync<BenchmarkDto, BenchmarkDto>(
            _two,
            32,
            32).ConfigureAwait(false);
        return result.Set1.Length + result.Set2.Length;
    }

    /// <summary>Exercises five contiguous SQL result sets through the public generated API.</summary>
    [Benchmark]
    public async ValueTask<int> ReadFiveResultSets()
    {
        var result = await _database.QueryMultipleImmutableArrayAsync<
            BenchmarkDto,
            BenchmarkDto,
            BenchmarkDto,
            BenchmarkDto,
            BenchmarkDto>(
            _five,
            32,
            32,
            32,
            32,
            32).ConfigureAwait(false);
        return result.Set1.Length + result.Set2.Length + result.Set3.Length + result.Set4.Length + result.Set5.Length;
    }

    /// <summary>Exercises the maximum v12 multi-result arity and all nine reader transitions.</summary>
    [Benchmark]
    public async ValueTask<int> ReadTenResultSets()
    {
        var result = await _database.QueryMultipleImmutableArrayAsync<
            BenchmarkDto,
            BenchmarkDto,
            BenchmarkDto,
            BenchmarkDto,
            BenchmarkDto,
            BenchmarkDto,
            BenchmarkDto,
            BenchmarkDto,
            BenchmarkDto,
            BenchmarkDto>(
            _ten,
            32,
            32,
            32,
            32,
            32,
            32,
            32,
            32,
            32,
            32).ConfigureAwait(false);
        return result.Set1.Length + result.Set2.Length + result.Set3.Length + result.Set4.Length + result.Set5.Length +
               result.Set6.Length + result.Set7.Length + result.Set8.Length + result.Set9.Length + result.Set10.Length;
    }

    private static StoredProcedureCommand CreateCommand(int resultSetCount)
    {
        return new StoredProcedureCommandBuilder("dbo", "caerius_benchmark_reader_" +
                                                        resultSetCount.ToString(CultureInfo.InvariantCulture))
            .WithResultSetCapacities(Enumerable.Repeat(32, resultSetCount).ToArray())
            .Build();
    }
}
