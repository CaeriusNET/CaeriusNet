namespace CaeriusNet.Tests.Caches;

/// <summary>
///     Unit tests for the public <see cref="ICaeriusNetCache" /> invalidation façade and the
///     newly-added Remove/Clear surface on each underlying cache manager.
///     Placed in <see cref="FrozenCacheStateCollection" /> because <c>ClearAsync(CacheType.Frozen)</c>
///     resets the process-wide static <see cref="FrozenCacheManager" /> and must not run in
///     parallel with other tests that store/retrieve from it.
/// </summary>
[Collection(FrozenCacheStateCollection.Name)]
public sealed class CaeriusNetCacheInvalidationTests
{
    private static readonly TimeSpan LongExpiry = TimeSpan.FromHours(1);

    [Fact]
    public void InMemoryCache_Remove_DeletesEntry()
    {
        var key = $"invalidate_inmem_{Guid.NewGuid()}";
        InMemoryCacheManager.Store(key, 7, LongExpiry);
        Assert.True(InMemoryCacheManager.TryGet<int>(key, out _));

        InMemoryCacheManager.Remove(key);

        Assert.False(InMemoryCacheManager.TryGet<int>(key, out _));
    }

    [Fact]
    public void InMemoryCache_Remove_UnknownKey_IsNoop()
    {
        var key = $"invalidate_unknown_{Guid.NewGuid()}";

        InMemoryCacheManager.Remove(key);

        Assert.False(InMemoryCacheManager.TryGet<int>(key, out _));
    }

    [Fact]
    public void FrozenCache_Remove_DeletesEntry()
    {
        var key = $"invalidate_frozen_{Guid.NewGuid()}";
        FrozenCacheManager.Store(key, 99);
        Assert.True(FrozenCacheManager.TryGet<int>(key, out _));

        FrozenCacheManager.Remove(key);

        Assert.False(FrozenCacheManager.TryGet<int>(key, out _));
    }

    [Fact]
    public void FrozenCache_Remove_LeavesOtherEntries()
    {
        var keep = $"invalidate_keep_{Guid.NewGuid()}";
        var drop = $"invalidate_drop_{Guid.NewGuid()}";
        FrozenCacheManager.Store(keep, "K");
        FrozenCacheManager.Store(drop, "D");

        FrozenCacheManager.Remove(drop);

        Assert.True(FrozenCacheManager.TryGet<string>(keep, out var kept));
        Assert.Equal("K", kept);
        Assert.False(FrozenCacheManager.TryGet<string>(drop, out _));
    }

    [Fact]
    public async Task Cache_RemoveAsync_AcrossAllTiers_RemovesFromBothLocalTiers()
    {
        var key = $"invalidate_facade_{Guid.NewGuid()}";
        FrozenCacheManager.Store(key, 1);
        InMemoryCacheManager.Store(key, 1, LongExpiry);

        var redis = new RecordingRedisCacheManager();
        ICaeriusNetCache facade = new CaeriusNetCache(redis);
        await facade.RemoveAsync(key);

        Assert.False(FrozenCacheManager.TryGet<int>(key, out _));
        Assert.False(InMemoryCacheManager.TryGet<int>(key, out _));
        Assert.Equal(new[] { key }, redis.RemovedKeys);
    }

    [Fact]
    public async Task Cache_RemoveAsync_TargetedTier_DoesNotTouchOtherTier()
    {
        var key = $"invalidate_targeted_{Guid.NewGuid()}";
        FrozenCacheManager.Store(key, "F");
        InMemoryCacheManager.Store(key, "M", LongExpiry);

        ICaeriusNetCache facade = new CaeriusNetCache();
        await facade.RemoveAsync(key, CacheType.InMemory);

        Assert.True(FrozenCacheManager.TryGet<string>(key, out _));
        Assert.False(InMemoryCacheManager.TryGet<string>(key, out _));
    }

    [Fact]
    public async Task Cache_RemoveAsync_TargetedRedis_CallsRedisOnly()
    {
        var key = $"invalidate_redis_{Guid.NewGuid()}";
        FrozenCacheManager.Store(key, "F");
        InMemoryCacheManager.Store(key, "M", LongExpiry);

        var redis = new RecordingRedisCacheManager();
        ICaeriusNetCache facade = new CaeriusNetCache(redis);
        await facade.RemoveAsync(key, CacheType.Redis);

        Assert.True(FrozenCacheManager.TryGet<string>(key, out _));
        Assert.True(InMemoryCacheManager.TryGet<string>(key, out _));
        Assert.Equal(new[] { key }, redis.RemovedKeys);
    }

    [Fact]
    public async Task Cache_ClearAsync_Frozen_EmptiesTier()
    {
        var key = $"invalidate_clear_frozen_{Guid.NewGuid()}";
        FrozenCacheManager.Store(key, 42);

        ICaeriusNetCache facade = new CaeriusNetCache();
        await facade.ClearAsync(CacheType.Frozen);

        Assert.False(FrozenCacheManager.TryGet<int>(key, out _));
    }

    [Fact]
    public async Task Cache_ClearAsync_InMemory_EmptiesTier()
    {
        var key = $"invalidate_clear_memory_{Guid.NewGuid()}";
        InMemoryCacheManager.Store(key, 42, LongExpiry);

        ICaeriusNetCache facade = new CaeriusNetCache();
        await facade.ClearAsync(CacheType.InMemory);

        Assert.False(InMemoryCacheManager.TryGet<int>(key, out _));
    }

    [Fact]
    public async Task Cache_ClearAsync_Redis_Throws()
    {
        ICaeriusNetCache facade = new CaeriusNetCache();
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            facade.ClearAsync(CacheType.Redis).AsTask());
    }

    [Fact]
    public async Task Cache_RemoveAsync_NullKey_Throws()
    {
        ICaeriusNetCache facade = new CaeriusNetCache();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            facade.RemoveAsync(null!).AsTask());
    }

    [Fact]
    public async Task Cache_RemoveAsync_EmptyKey_Throws()
    {
        ICaeriusNetCache facade = new CaeriusNetCache();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            facade.RemoveAsync(string.Empty).AsTask());
    }

    [Fact]
    public async Task Cache_RemoveAsync_WhitespaceKey_Throws()
    {
        ICaeriusNetCache facade = new CaeriusNetCache();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            facade.RemoveAsync("   ").AsTask());
    }

    [Fact]
    public async Task Cache_RemoveAsync_CanceledToken_DoesNotTouchTiers()
    {
        var key = $"invalidate_canceled_{Guid.NewGuid()}";
        FrozenCacheManager.Store(key, 5);
        InMemoryCacheManager.Store(key, 5, LongExpiry);
        var redis = new RecordingRedisCacheManager();
        ICaeriusNetCache facade = new CaeriusNetCache(redis);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            facade.RemoveAsync(key, cts.Token).AsTask());

        Assert.True(FrozenCacheManager.TryGet<int>(key, out _));
        Assert.True(InMemoryCacheManager.TryGet<int>(key, out _));
        Assert.Empty(redis.RemovedKeys);
    }

    [Fact]
    public async Task Cache_ClearAsync_CanceledToken_DoesNotClearTier()
    {
        var key = $"invalidate_clear_canceled_{Guid.NewGuid()}";
        FrozenCacheManager.Store(key, 42);
        ICaeriusNetCache facade = new CaeriusNetCache();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            facade.ClearAsync(CacheType.Frozen, cts.Token).AsTask());

        Assert.True(FrozenCacheManager.TryGet<int>(key, out _));
    }

    [Fact]
    public async Task Cache_RemoveAsync_UnknownTierEnum_Throws()
    {
        ICaeriusNetCache facade = new CaeriusNetCache();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            facade.RemoveAsync("k", (CacheType)42).AsTask());
    }

    private sealed class RecordingRedisCacheManager : IRedisCacheManager
    {
        public List<string> RemovedKeys { get; } = [];

        public bool TryGet<T>(string cacheKey, out T? value)
        {
            value = default;
            return false;
        }

        public void Store<T>(string cacheKey, T value, TimeSpan? expiration)
            where T : notnull
        {
        }

        public void Remove(string cacheKey)
        {
            RemovedKeys.Add(cacheKey);
        }
    }
}
