namespace CaeriusNet.Tests.Factories;

/// <summary>
///     Unit tests for <see cref="CaeriusNetDbContext" /> constructor validation and property assignment.
/// </summary>
public sealed class CaeriusNetDbContextTests
{
    [Fact]
    public void Constructor_NullFactory_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CaeriusNetDbContext(null!));
    }

    [Fact]
    public void Constructor_WithFactory_SetsRedisCacheManager()
    {
        var redis = new FakeRedisCacheManager();
        var ctx = new CaeriusNetDbContext(() => new SqlConnection(), redis);

        Assert.Same(redis, ctx.RedisCacheManager);
    }

    [Fact]
    public void Constructor_WithNullRedis_SetsNull()
    {
        var ctx = new CaeriusNetDbContext(() => new SqlConnection());

        Assert.Null(ctx.RedisCacheManager);
    }

    [Fact]
    public async Task DbConnectionAsync_FactoryReturnsNull_Throws_InvalidOperationException()
    {
        var ctx = new CaeriusNetDbContext(() => null!);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.DbConnectionAsync().AsTask());

        Assert.Contains("returned null", ex.Message);
    }

    [Fact]
    public async Task DbConnectionAsync_OpenAsyncThrows_Rethrows_Original_Exception()
    {
        var connection = new SqlConnection();
        var ctx = new CaeriusNetDbContext(() => connection);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.DbConnectionAsync().AsTask());

        Assert.Contains("ConnectionString", ex.Message);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    private sealed class FakeRedisCacheManager : IRedisCacheManager
    {
        public bool TryGet<T>(string cacheKey, out T? value)
        {
            value = default;
            return false;
        }

        public void Store<T>(string cacheKey, T value, TimeSpan? expiration) where T : notnull
        {
        }

        public void Remove(string cacheKey)
        {
        }
    }
}
