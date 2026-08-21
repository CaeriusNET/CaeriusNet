using System.Security.Cryptography;
using CaeriusNet.Benchmark.Infrastructure;
using CaeriusNet.Redis;
using StackExchange.Redis;

namespace CaeriusNet.Benchmark.Caching;

/// <summary>
///     Opt-in, end-to-end Redis read-through benchmarks. This class uses the production Redis implementation and a
///     real SQL reader rather than a mocked remote cache; it is intentionally excluded from the portable benchmark
///     category because it requires dedicated disposable SQL Server and Redis instances.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("redis", "cache", "sql-server", "integration")]
public class RedisReadThroughBenchmarks
{
    private const string DependencyTag = "benchmark:redis:widget";
    private string _cacheKey = string.Empty;
    private StoredProcedureCommand _command = null!;

    private ICaeriusNetDbContext _database = null!;
    private string _databaseIdentity = string.Empty;
    private ConnectionMultiplexer _multiplexer = null!;
    private IRedisCacheManager _redis = null!;
    private IDatabase _redisDatabase = null!;
    private string _redisKeyPrefix = string.Empty;
    private IServiceScope _scope = null!;
    private ServiceProvider _services = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        var sqlConnectionString = BenchmarkEnvironment.RequireSqlServerConnectionString();
        var redisConnectionString = BenchmarkEnvironment.RequireRedisConnectionString();
        await BenchmarkSqlServerDatabase.ProvisionAsync(sqlConnectionString).ConfigureAwait(false);

        _multiplexer = await ConnectionMultiplexer.ConnectAsync(redisConnectionString).ConfigureAwait(false);
        _redisDatabase = _multiplexer.GetDatabase();
        var runIdentifier = Guid.NewGuid().ToString("N");
        _databaseIdentity = "benchmark-redis-" + runIdentifier;
        _redisKeyPrefix = "caerius-benchmark:" + runIdentifier + ':';

        var services = new ServiceCollection();
        services.AddCaeriusNet(options =>
        {
            options.UseSqlServer(sqlConnectionString);
            options.ApplicationNamespace = "caerius-benchmark";
            options.CacheDatabaseIdentity = _databaseIdentity;
        });
        services.AddSingleton<IConnectionMultiplexer>(_multiplexer);
        services.AddCaeriusNetRedis(options => options.KeyPrefix = _redisKeyPrefix);
        services.AddCaeriusCacheCodec<BenchmarkDto, BenchmarkDtoCaeriusCacheCodec>();
        _services = services.BuildServiceProvider();
        _scope = _services.CreateScope();
        _database = _scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        _redis = _scope.ServiceProvider.GetRequiredService<IRedisCacheManager>();

        _command = new StoredProcedureCommandBuilder("dbo", "caerius_benchmark_redis_reader")
            .DependsOn(DependencyTag)
            .UseRedisCache(TimeSpan.FromMinutes(5))
            .Build();
        var codec = _scope.ServiceProvider.GetRequiredService<ICaeriusCacheCodec<ImmutableArray<BenchmarkDto>>>();
        _cacheKey = CaeriusCacheKeyBuilder.Create(
            "caerius-benchmark",
            _databaseIdentity,
            _command,
            "immutable:" + typeof(BenchmarkDto).FullName,
            codec.ContractHash);

        var populated = await _database.QueryImmutableArrayAsync<BenchmarkDto>(_command, 32).ConfigureAwait(false);
        if (populated.Length != 32)
            throw new InvalidOperationException(
                "The Redis benchmark procedure did not return its expected 32 DTO rows.");
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(_cacheKey))
                await _redis.RemoveAsync(_cacheKey).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(_redisKeyPrefix))
                await _redisDatabase.KeyDeleteAsync(_redisKeyPrefix + GetGenerationKey(DependencyTag))
                    .ConfigureAwait(false);
        }
        finally
        {
            _scope.Dispose();
            _services.Dispose();
            await _multiplexer.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Measures the complete production tagged Redis hit: generation snapshot, Lua validation and codec decode.</summary>
    [Benchmark(Baseline = true)]
    public async ValueTask<int> TaggedRedisHit()
    {
        var result = await _database.QueryImmutableArrayAsync<BenchmarkDto>(_command, 32).ConfigureAwait(false);
        return result.Length;
    }

    /// <summary>Measures a real Redis miss, SQL reader fallback and atomic tagged population; cleanup is outside timing.</summary>
    [Benchmark]
    public async ValueTask<int> TaggedRedisMissAndPopulate()
    {
        var result = await _database.QueryImmutableArrayAsync<BenchmarkDto>(_command, 32).ConfigureAwait(false);
        return result.Length;
    }

    /// <summary>Measures the Lua generation increment used after a durable SQL write commits.</summary>
    [Benchmark]
    public ValueTask InvalidateRedisDependencyTag()
    {
        return _redis.InvalidateTagsAsync([DependencyTag]);
    }

    [IterationSetup(Target = nameof(TaggedRedisMissAndPopulate))]
    public void RemovePayloadBeforeMiss()
    {
        // Iteration setup is deliberately outside the measurement. It removes exactly this run's payload, not a
        // shared database or a broad key pattern, and forces the public query API through the SQL+publish path.
        _redis.RemoveAsync(_cacheKey).AsTask().GetAwaiter().GetResult();
    }

    private static string GetGenerationKey(string tag)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(tag));
        return "caerius:v2:tag:" + Convert.ToHexStringLower(hash);
    }
}
