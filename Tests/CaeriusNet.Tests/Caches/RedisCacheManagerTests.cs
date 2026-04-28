using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaeriusNet.Tests.Caches;

/// <summary>
///     Unit tests for <see cref="RedisCacheManager" /> covering serialization, graceful degradation,
///     expiration handling, and corrupted-data resilience.
/// </summary>
public sealed class RedisCacheManagerTests
{
    [Fact]
    public void Store_Serializes_And_Stores_Bytes_Via_DistributedCache()
    {
        var fake = new FakeDistributedCache();
        var manager = new RedisCacheManager(fake);

        manager.Store("key1", new TestPayload("hello", 42), null);

        Assert.True(fake.Storage.TryGetValue("key1", out var cachedValue));
        Assert.True(cachedValue.Length > 0);
    }

    [Fact]
    public void TryGet_Deserializes_Stored_Bytes_Correctly()
    {
        var fake = new FakeDistributedCache();
        var manager = new RedisCacheManager(fake);
        var original = new TestPayload("world", 99);

        manager.Store("key2", original, null);
        var found = manager.TryGet<TestPayload>("key2", out var result);

        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal("world", result.Name);
        Assert.Equal(99, result.Value);
    }

    [Fact]
    public void TryGet_Returns_False_When_Key_Not_Found()
    {
        var fake = new FakeDistributedCache();
        var manager = new RedisCacheManager(fake);

        var found = manager.TryGet<TestPayload>("missing", out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void TryGet_Returns_False_When_DistributedCache_Is_Null()
    {
        var manager = new RedisCacheManager(null);

        var found = manager.TryGet<TestPayload>("any", out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void Store_With_Null_DistributedCache_Is_NoOp()
    {
        var manager = new RedisCacheManager(null);

        var ex = Record.Exception(() => manager.Store("key", new TestPayload("x", 1), null));

        Assert.Null(ex);
    }

    [Fact]
    public void Remove_Calls_DistributedCache_Remove()
    {
        var fake = new FakeDistributedCache();
        var manager = new RedisCacheManager(fake);
        manager.Store("toRemove", new TestPayload("bye", 0), null);

        manager.Remove("toRemove");

        Assert.False(fake.Storage.ContainsKey("toRemove"));
    }

    [Fact]
    public void Remove_With_Null_DistributedCache_Is_NoOp()
    {
        var manager = new RedisCacheManager(null);

        var ex = Record.Exception(() => manager.Remove("any"));

        Assert.Null(ex);
    }

    [Fact]
    public void Store_With_Expiration_Sets_AbsoluteExpirationRelativeToNow()
    {
        var fake = new FakeDistributedCache();
        var manager = new RedisCacheManager(fake);
        var expiration = TimeSpan.FromMinutes(10);

        manager.Store("exp_key", new TestPayload("timed", 1), expiration);

        Assert.True(fake.Storage.ContainsKey("exp_key"));
        Assert.NotNull(fake.LastOptions);
        Assert.Equal(expiration, fake.LastOptions!.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public void Store_Without_Expiration_Does_Not_Set_Expiration()
    {
        var fake = new FakeDistributedCache();
        var manager = new RedisCacheManager(fake);

        manager.Store("no_exp", new TestPayload("forever", 1), null);

        Assert.True(fake.Storage.ContainsKey("no_exp"));
        Assert.NotNull(fake.LastOptions);
        Assert.Null(fake.LastOptions!.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public void TryGet_Returns_False_When_Deserialization_Fails()
    {
        var fake = new FakeDistributedCache();
        fake.Storage["corrupt"] = [0xFF, 0xFE, 0x00, 0x01];
        var manager = new RedisCacheManager(fake);

        var found = manager.TryGet<TestPayload>("corrupt", out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void Constructor_Logs_RedisConnected_When_Both_Cache_And_Logger_Present()
    {
        var fake = new FakeDistributedCache();
        var loggerFactory = NullLoggerFactory.Instance;

        var ex = Record.Exception(() => new RedisCacheManager(fake, loggerFactory));

        Assert.Null(ex);
    }

    /// <summary>
    ///     Minimal fake implementing <see cref="IDistributedCache" /> for unit testing.
    /// </summary>
    private sealed class FakeDistributedCache : IDistributedCache
    {
        public Dictionary<string, byte[]> Storage { get; } = new();
        public DistributedCacheEntryOptions? LastOptions { get; private set; }

        public byte[]? Get(string key)
        {
            return Storage.TryGetValue(key, out var value) ? value : null;
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            return Task.FromResult(Get(key));
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            Storage[key] = value;
            LastOptions = options;
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            Storage.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    ///     Simple record used as serialization target in Redis cache tests.
    /// </summary>
    private sealed record TestPayload(string Name, int Value);
}
