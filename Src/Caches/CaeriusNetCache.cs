namespace CaeriusNet.Caches;

/// <summary>
///     Default implementation of <see cref="ICaeriusNetCache" />. Delegates each operation to the
///     appropriate internal cache manager. Registered as a singleton by <see cref="Builders.CaeriusNetBuilder" />.
/// </summary>
internal sealed class CaeriusNetCache(IRedisCacheManager? redisCacheManager = null) : ICaeriusNetCache
{
    public ValueTask RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheKey);
        cancellationToken.ThrowIfCancellationRequested();

        FrozenCacheManager.Remove(cacheKey);
        InMemoryCacheManager.Remove(cacheKey);
        redisCacheManager?.Remove(cacheKey);

        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string cacheKey, CacheType cacheType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(cacheKey);
        cancellationToken.ThrowIfCancellationRequested();

        switch (cacheType)
        {
            case Frozen:
                FrozenCacheManager.Remove(cacheKey);
                break;
            case InMemory:
                InMemoryCacheManager.Remove(cacheKey);
                break;
            case Redis:
                redisCacheManager?.Remove(cacheKey);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cacheType), cacheType, "Unsupported cache type.");
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(CacheType cacheType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (cacheType)
        {
            case Frozen:
                FrozenCacheManager.Clear();
                break;
            case InMemory:
                InMemoryCacheManager.Clear();
                break;
            case Redis:
                throw new NotSupportedException(
                    "Clearing Redis is not supported. Use RemoveAsync(key, CacheType.Redis) for targeted invalidation.");
            default:
                throw new ArgumentOutOfRangeException(nameof(cacheType), cacheType, "Unsupported cache type.");
        }

        return ValueTask.CompletedTask;
    }
}
