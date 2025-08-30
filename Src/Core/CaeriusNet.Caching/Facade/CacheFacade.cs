namespace CaeriusNet.Core.Caching.Facade;

/// <summary>
///     Public facade that unifies access to available cache stores (InMemory, Frozen, Redis) behind a minimal, async-first
///     API.
///     All operations are cancellation-aware and optimized for low overhead; cache paths are non-authoritative by design.
/// </summary>
/// <remarks>
///     Usage contract:
///     - For CacheType.Redis, the application must have registered Redis via AddRedisCache and wired the service provider
///     into the caching layer using RedisCacheStore.SetServiceProvider(...). If not, attempting to use Redis throws.
///     - InMemory and Frozen are process-local and do not require DI.
/// </remarks>
public static class CacheFacade
{
    /// <summary>
    ///     Wires the application's root service provider into the Redis cache store.
    ///     Must be called once at startup if <see cref="CacheType.Redis" /> will be used.
    /// </summary>
    /// <param name="serviceProvider">The application root service provider.</param>
    public static void UseServiceProvider(IServiceProvider serviceProvider)
    {
        RedisCacheStore.SetServiceProvider(serviceProvider);
    }

    /// <summary>
    ///     Attempts to retrieve a cached value for the given key.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="cacheType">Target cache type.</param>
    /// <param name="cacheKey">Deterministic cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>(Found, Value) tuple; Found=false on miss or error.</returns>
    public static ValueTask<(bool Found, T? Value)> TryGetAsync<T>(CacheType cacheType, string cacheKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return cacheType switch
            {
                CacheType.InMemory => new ValueTask<(bool, T?)>(InMemoryCacheStore.TryGet(cacheKey, out T? v1)
                    ? (true, v1)
                    : (false, default)),
                CacheType.Frozen => new ValueTask<(bool, T?)>(FrozenCacheStore.TryGet(cacheKey, out T? v2)
                    ? (true, v2)
                    : (false, default)),
                CacheType.Redis => TryGetRedis(cacheKey),
                _ => new ValueTask<(bool, T?)>((false, default))
            };
        }
        catch
        {
            return new ValueTask<(bool, T?)>((false, default));
        }

        static ValueTask<(bool, T?)> TryGetRedis(string key)
        {
            return !RedisCacheStore.IsInitialized()
                ? throw new InvalidOperationException(
                    "Redis cache is not initialized. Ensure AddRedisCache() is registered and CacheFacade.UseServiceProvider(...) was called before using CacheType.Redis.")
                : new ValueTask<(bool, T?)>(RedisCacheStore.TryGet(key, out T? v) ? (true, v) : (false, default));
        }
    }

    /// <summary>
    ///     Stores a value into the selected cache with an optional expiration (ignored by Frozen).
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="cacheType">Target cache type.</param>
    /// <param name="cacheKey">Deterministic cache key.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="expiration">Optional TTL (ignored for Frozen).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask StoreAsync<T>(CacheType cacheType, string cacheKey, T value, TimeSpan? expiration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            switch (cacheType)
            {
                case CacheType.InMemory:
                    if (expiration is { } ttl1) InMemoryCacheStore.Store(cacheKey, value, ttl1);
                    break;

                case CacheType.Frozen:
                    FrozenCacheStore.Store(cacheKey, value);
                    break;

                case CacheType.Redis:
                    if (!RedisCacheStore.IsInitialized())
                        throw new InvalidOperationException(
                            "Redis cache is not initialized. Ensure AddRedisCache() is registered and CacheFacade.UseServiceProvider(...) was called before using CacheType.Redis.");
                    RedisCacheStore.Store(cacheKey, value, expiration);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cacheType), cacheType, null);
            }
        }
        catch
        {
            // Non-authoritative path: ignore cache failures.
        }

        return ValueTask.CompletedTask;
    }
}