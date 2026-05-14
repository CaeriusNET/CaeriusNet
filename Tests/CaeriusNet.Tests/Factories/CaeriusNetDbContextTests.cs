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