using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using CaeriusNet.Execution;
using CaeriusNet.IntegrationTests.Infrastructure;
using CaeriusNet.Mappers;

namespace CaeriusNet.IntegrationTests.Redis;

/// <summary>
///     Exercises the production read-through path end-to-end: stored procedure, typed codec, Redis envelope,
///     cache invalidation and aggregate multi-result payload all run against disposable SQL Server and Redis containers.
/// </summary>
public sealed class SqlRedisReadThroughAcceptanceTests
{
    [Theory]
    [MemberData(nameof(ContainerTestGate.SqlServerImages), MemberType = typeof(ContainerTestGate))]
    public async Task SqlAndRedisReadThroughHonorsCachingConcurrencyCompatibilityAndInvalidation(string image)
    {
        ContainerTestGate.RequireEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var sqlContainer = new MsSqlBuilder(image)
            .WithPassword("Caerius!Tests_2026")
            .Build();
        await using var redisContainer = new RedisBuilder("redis:7.4-alpine").Build();
        await Task.WhenAll(sqlContainer.StartAsync(cancellationToken), redisContainer.StartAsync(cancellationToken));

        var connectionString = new SqlConnectionStringBuilder(sqlContainer.GetConnectionString())
        {
            TrustServerCertificate = true
        }.ConnectionString;
        await ProvisionAsync(connectionString, cancellationToken);

        var redisOptions = ConfigurationOptions.Parse(redisContainer.GetConnectionString());
        redisOptions.AllowAdmin = true;
        using var multiplexer = await ConnectionMultiplexer.ConnectAsync(redisOptions);
        var redis = multiplexer.GetDatabase();
        await redis.ExecuteAsync("FLUSHDB");

        await using var provider = BuildProvider(connectionString, multiplexer);

        await AssertReadThroughAndTtlAsync(provider, connectionString, cancellationToken);
        await AssertConcurrentReadThroughUsesOneLocalPopulationAsync(provider, connectionString, redis,
            cancellationToken);
        await AssertCorruptionAndCodecCompatibilityAsync(provider, connectionString, redis, cancellationToken);
        await AssertStrictInvalidationAsync(provider, redis, cancellationToken);
        await AssertMultiResultAggregateIsStoredUnderOneKeyAsync(provider, connectionString, redis, cancellationToken);
        await AssertCancelledReadThroughWaiterDoesNotCancelSharedPopulationAsync(provider, connectionString, redis,
            cancellationToken);
        await AssertLookupCancellationDoesNotLeaveTheConnectionUnusableAsync(provider, redis, cancellationToken);
        await AssertFailOpenAndStrictFailurePoliciesAsync(connectionString, multiplexer, cancellationToken);
    }

    private static ServiceProvider BuildProvider(string connectionString, IConnectionMultiplexer multiplexer)
    {
        var services = new ServiceCollection();
        services.AddCaeriusNet(options => options.UseSqlServer(connectionString));
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddCaeriusNetRedis();
        services.AddCaeriusCacheCodec<CacheWidget, CacheWidgetCodec>();
        return services.BuildServiceProvider();
    }

    private static async Task AssertReadThroughAndTtlAsync(
        ServiceProvider provider,
        string connectionString,
        CancellationToken cancellationToken)
    {
        await ResetCountersAsync(connectionString, cancellationToken);
        await provider.GetRequiredService<IConnectionMultiplexer>().GetDatabase().ExecuteAsync("FLUSHDB");
        var command = CachedWidgetCommand("caerius_redis_read_widget", TimeSpan.FromMilliseconds(250));

        await using (var scope = provider.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
            Assert.Equal(7,
                Assert.Single(
                        await database.QueryImmutableArrayAsync<CacheWidget>(command,
                            cancellationToken: cancellationToken))
                    .Id);
            Assert.Equal(7,
                Assert.Single(
                        await database.QueryImmutableArrayAsync<CacheWidget>(command,
                            cancellationToken: cancellationToken))
                    .Id);
        }

        Assert.Equal(1, await ReadCounterAsync(connectionString, "read", cancellationToken));

        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        await using (var scope = provider.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
            Assert.Equal(7,
                Assert.Single(
                        await database.QueryImmutableArrayAsync<CacheWidget>(command,
                            cancellationToken: cancellationToken))
                    .Id);
        }

        Assert.Equal(2, await ReadCounterAsync(connectionString, "read", cancellationToken));
    }

    private static async Task AssertConcurrentReadThroughUsesOneLocalPopulationAsync(
        ServiceProvider provider,
        string connectionString,
        IDatabase redis,
        CancellationToken cancellationToken)
    {
        await ResetCountersAsync(connectionString, cancellationToken);
        await redis.ExecuteAsync("FLUSHDB");
        var command = CachedWidgetCommand("caerius_redis_read_widget_slow", TimeSpan.FromMinutes(1));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new CountdownEvent(32);

        var calls = Enumerable.Range(0, 32).Select(async _ =>
        {
            await using var scope = provider.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
            ready.Signal();
            await start.Task.WaitAsync(cancellationToken);
            return await database.QueryImmutableArrayAsync<CacheWidget>(command, cancellationToken: cancellationToken);
        }).ToArray();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(5), cancellationToken),
            "All concurrent read-through callers should be ready before the SQL procedure is released.");
        start.TrySetResult();
        var results = await Task.WhenAll(calls);
        Assert.All(results, static result => Assert.Equal(7, Assert.Single(result).Id));
        Assert.Equal(1, await ReadCounterAsync(connectionString, "slow", cancellationToken));
        Assert.Equal(1L, await DatabaseSizeAsync(redis));
    }

    private static async Task AssertCorruptionAndCodecCompatibilityAsync(
        ServiceProvider provider,
        string connectionString,
        IDatabase redis,
        CancellationToken cancellationToken)
    {
        await ResetCountersAsync(connectionString, cancellationToken);
        await redis.ExecuteAsync("FLUSHDB");
        var command = CachedWidgetCommand("caerius_redis_read_widget", TimeSpan.FromMinutes(1));
        await using (var scope = provider.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
            _ = await database.QueryImmutableArrayAsync<CacheWidget>(command, cancellationToken: cancellationToken);
        }

        var cachedKey = Assert.Single(await GetCaeriusKeysAsync(redis));
        await redis.StringSetAsync(cachedKey, new byte[] { 0xC0, 0xFF, 0xEE });
        await using (var scope = provider.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
            _ = await database.QueryImmutableArrayAsync<CacheWidget>(command, cancellationToken: cancellationToken);
        }

        Assert.Equal(2, await ReadCounterAsync(connectionString, "read", cancellationToken));
        Assert.Single(await GetCaeriusKeysAsync(redis));

        var manager = provider.GetRequiredService<IRedisCacheManager>();
        var versionOne = new Int32Codec("sql-redis-counter", "v1");
        var incompatibleKey = "sql-redis-incompatible";
        await manager.SetAsync(incompatibleKey, 42, versionOne, TimeSpan.FromMinutes(1), cancellationToken);
        var lookup = await manager.TryGetAsync(incompatibleKey, new Int32Codec("sql-redis-counter", "v2"),
            cancellationToken);
        Assert.False(lookup.Found);
        Assert.False(await redis.KeyExistsAsync("caerius:" + incompatibleKey));
    }

    private static async Task AssertStrictInvalidationAsync(
        ServiceProvider provider,
        IDatabase redis,
        CancellationToken cancellationToken)
    {
        await redis.StringSetAsync("caerius:strict-invalidate", "value");
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var command = new StoredProcedureCommandBuilder("dbo", "caerius_redis_invalidate")
            .Invalidate("strict-invalidate")
            .Build();

        await database.ExecuteAsync(command, cancellationToken);
        Assert.False(await redis.KeyExistsAsync("caerius:strict-invalidate"));
    }

    private static async Task AssertMultiResultAggregateIsStoredUnderOneKeyAsync(
        ServiceProvider provider,
        string connectionString,
        IDatabase redis,
        CancellationToken cancellationToken)
    {
        await ResetCountersAsync(connectionString, cancellationToken);
        await redis.ExecuteAsync("FLUSHDB");
        var command = new StoredProcedureCommandBuilder("dbo", "caerius_redis_read_multi")
            .WithResultSetCapacities(1, 1)
            .UseRedisCache(TimeSpan.FromMinutes(1))
            .Build();

        await using (var scope = provider.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
            var first = await database.QueryMultipleImmutableArrayAsync<CacheWidget, CacheWidget>(
                command,
                cancellationToken: cancellationToken);
            Assert.Equal(1, Assert.Single(first.Set1).Id);
            Assert.Equal(2, Assert.Single(first.Set2).Id);

            var cached = await database.QueryMultipleImmutableArrayAsync<CacheWidget, CacheWidget>(
                command,
                cancellationToken: cancellationToken);
            Assert.Equal(1, Assert.Single(cached.Set1).Id);
            Assert.Equal(2, Assert.Single(cached.Set2).Id);
        }

        Assert.Equal(1, await ReadCounterAsync(connectionString, "multi", cancellationToken));
        Assert.Equal(1L, await DatabaseSizeAsync(redis));
    }

    private static async Task AssertLookupCancellationDoesNotLeaveTheConnectionUnusableAsync(
        ServiceProvider provider,
        IDatabase redis,
        CancellationToken cancellationToken)
    {
        await redis.ExecuteAsync("FLUSHDB");
        var manager = provider.GetRequiredService<IRedisCacheManager>();
        var codec = new Int32Codec("sql-redis-cancellation", "v1");
        await manager.SetAsync("cancelled-lookup", 9, codec, TimeSpan.FromMinutes(1), cancellationToken);

        _ = await redis.ExecuteAsync("CLIENT", "PAUSE", "1000");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            manager.TryGetAsync("cancelled-lookup", codec, cancellation.Token).AsTask());

        await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken);
        var recovered = await manager.TryGetAsync("cancelled-lookup", codec, cancellationToken);
        Assert.True(recovered.Found);
        Assert.Equal(9, recovered.Value);
    }

    private static async Task AssertCancelledReadThroughWaiterDoesNotCancelSharedPopulationAsync(
        ServiceProvider provider,
        string connectionString,
        IDatabase redis,
        CancellationToken cancellationToken)
    {
        await ResetCountersAsync(connectionString, cancellationToken);
        await redis.ExecuteAsync("FLUSHDB");
        var command = CachedWidgetCommand("caerius_redis_read_widget_slow", TimeSpan.FromMinutes(1));

        await using var cancelledScope = provider.CreateAsyncScope();
        await using var survivorScope = provider.CreateAsyncScope();
        var cancelledDatabase = cancelledScope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var survivorDatabase = survivorScope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(75));

        var cancelledWaiter = cancelledDatabase
            .QueryImmutableArrayAsync<CacheWidget>(command, cancellationToken: cancellation.Token)
            .AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        var survivingWaiter = survivorDatabase
            .QueryImmutableArrayAsync<CacheWidget>(command, cancellationToken: cancellationToken)
            .AsTask();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledWaiter);
        Assert.Equal(7, Assert.Single(await survivingWaiter).Id);
        Assert.Equal(1, await ReadCounterAsync(connectionString, "slow", cancellationToken));
        Assert.Equal(1L, await DatabaseSizeAsync(redis));
    }

    private static async Task AssertFailOpenAndStrictFailurePoliciesAsync(
        string connectionString,
        ConnectionMultiplexer multiplexer,
        CancellationToken cancellationToken)
    {
        await multiplexer.GetDatabase().ExecuteAsync("FLUSHDB");
        await ResetCountersAsync(connectionString, cancellationToken);
        var services = new ServiceCollection();
        services.AddCaeriusNet(options => options.UseSqlServer(connectionString));
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddCaeriusNetRedis();
        services.AddCaeriusCacheCodec<CacheWidget, CacheWidgetCodec>();
        services.AddSingleton<ICaeriusRemoteCacheManager, FailingRemoteCache>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var value = await database.QueryImmutableArrayAsync<CacheWidget>(
            CachedWidgetCommand("caerius_redis_read_widget", TimeSpan.FromMinutes(1)),
            cancellationToken: cancellationToken);
        Assert.Equal(7, Assert.Single(value).Id);
        Assert.Equal(1, await ReadCounterAsync(connectionString, "read", cancellationToken));

        var strict = new StoredProcedureCommandBuilder("dbo", "caerius_redis_invalidate")
            .Invalidate("strict-failure")
            .Build();
        await Assert.ThrowsAsync<CaeriusCacheException>(() =>
            database.ExecuteAsync(strict, cancellationToken).AsTask());
    }

    private static StoredProcedureCommand CachedWidgetCommand(string procedure, TimeSpan expiration)
    {
        return new StoredProcedureCommandBuilder("dbo", procedure)
            .UseRedisCache(expiration)
            .Build();
    }

    private static async Task ProvisionAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, """
                                       IF OBJECT_ID(N'dbo.CaeriusRedisAcceptanceCounter', N'U') IS NULL
                                           CREATE TABLE dbo.CaeriusRedisAcceptanceCounter (Name nvarchar(32) NOT NULL PRIMARY KEY, Value int NOT NULL);
                                       DELETE FROM dbo.CaeriusRedisAcceptanceCounter;
                                       INSERT dbo.CaeriusRedisAcceptanceCounter (Name, Value) VALUES (N'read', 0), (N'slow', 0), (N'multi', 0);
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       IF OBJECT_ID(N'dbo.CaeriusRedisAcceptanceWidget', N'U') IS NULL
                                           CREATE TABLE dbo.CaeriusRedisAcceptanceWidget (Id int NOT NULL PRIMARY KEY, Name nvarchar(80) NOT NULL);
                                       DELETE FROM dbo.CaeriusRedisAcceptanceWidget;
                                       INSERT dbo.CaeriusRedisAcceptanceWidget (Id, Name) VALUES (7, N'redis widget');
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_redis_read_widget
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           UPDATE dbo.CaeriusRedisAcceptanceCounter SET Value = Value + 1 WHERE Name = N'read';
                                           SELECT Id, Name FROM dbo.CaeriusRedisAcceptanceWidget;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_redis_read_widget_slow
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           UPDATE dbo.CaeriusRedisAcceptanceCounter SET Value = Value + 1 WHERE Name = N'slow';
                                           WAITFOR DELAY '00:00:00.300';
                                           SELECT Id, Name FROM dbo.CaeriusRedisAcceptanceWidget;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_redis_read_multi
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           UPDATE dbo.CaeriusRedisAcceptanceCounter SET Value = Value + 1 WHERE Name = N'multi';
                                           SELECT 1 AS Id, N'first' AS Name;
                                           SELECT 2 AS Id, N'second' AS Name;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_redis_invalidate
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                       END
                                       """, cancellationToken);
    }

    private static async Task ResetCountersAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE dbo.CaeriusRedisAcceptanceCounter SET Value = 0;", cancellationToken);
    }

    private static async Task<int> ReadCounterAsync(string connectionString, string name,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM dbo.CaeriusRedisAcceptanceCounter WHERE Name = @name;";
        command.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 32) { Value = name });
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string text, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = text;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> DatabaseSizeAsync(IDatabase database)
    {
        return (long)await database.ExecuteAsync("DBSIZE");
    }

    private static async Task<RedisKey[]> GetCaeriusKeysAsync(IDatabase database)
    {
        var response = await database.ExecuteAsync("KEYS", "caerius:*");
        if (response.IsNull)
            return [];

        var values = (RedisResult[]?)response;
        return values is null
            ? []
            : values.Select(static value => (RedisKey)value.ToString()).ToArray();
    }

    private sealed record CacheWidget(int Id, string Name) : ISpMapper<CacheWidget>
    {
        public static CacheWidget MapFromDataReader(SqlDataReader reader)
        {
            return new CacheWidget(reader.GetInt32(0), reader.GetString(1));
        }
    }

    private sealed class CacheWidgetCodec : ICaeriusCacheCodec<CacheWidget>
    {
        public string CodecId => "caerius.tests.sql-redis-widget";
        public string ContractHash => "d0b77777";

        public void Encode(IBufferWriter<byte> writer, CacheWidget value)
        {
            var binary = new CaeriusBinaryWriter(writer);
            binary.WriteInt32(value.Id);
            binary.WriteString(value.Name);
        }

        public CacheWidget Decode(ReadOnlySpan<byte> payload)
        {
            var binary = new CaeriusBinaryReader(payload);
            var value = new CacheWidget(binary.ReadInt32(), binary.ReadString());
            if (binary.Remaining != 0)
                throw new CaeriusContractException("The SQL/Redis cache widget payload contains trailing data.");
            return value;
        }
    }

    private sealed class Int32Codec(string codecId, string contractHash) : ICaeriusCacheCodec<int>
    {
        public string CodecId { get; } = codecId;
        public string ContractHash { get; } = contractHash;

        public void Encode(IBufferWriter<byte> writer, int value)
        {
            new CaeriusBinaryWriter(writer).WriteInt32(value);
        }

        public int Decode(ReadOnlySpan<byte> payload)
        {
            var binary = new CaeriusBinaryReader(payload);
            var value = binary.ReadInt32();
            if (binary.Remaining != 0)
                throw new CaeriusContractException("The SQL/Redis integer payload contains trailing data.");
            return value;
        }
    }

    private sealed class FailingRemoteCache : ICaeriusRemoteCacheManager
    {
        private static readonly InvalidOperationException
            Failure = new("The simulated Redis transport is unavailable.");

        public ValueTask<CaeriusCacheLookup<T>> TryGetAsync<T>(
            string key,
            ICaeriusCacheCodec<T> codec,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<CaeriusCacheLookup<T>>(Failure);
        }

        public ValueTask SetAsync<T>(
            string key,
            T value,
            ICaeriusCacheCodec<T> codec,
            TimeSpan? expiration,
            CancellationToken cancellationToken = default) where T : notnull
        {
            return ValueTask.FromException(Failure);
        }

        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException(Failure);
        }
    }
}
