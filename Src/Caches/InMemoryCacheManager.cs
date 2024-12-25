using Microsoft.Extensions.Caching.Memory;

namespace CaeriusNet.Caches;

/// <summary>
///     Provides static methods for managing in-memory caching of data.
/// </summary>
public static class InMemoryCacheManager
{
    private static readonly MemoryCache MemoryCache = new(new MemoryCacheOptions());

    /// <summary>
    ///     Stores the specified value in the in-memory cache with the given cache key and expiration time.
    /// </summary>
    /// <typeparam name="T">The type of the value to be stored in the cache.</typeparam>
    /// <param name="cacheKey">The unique key used to store and retrieve the value from the cache.</param>
    /// <param name="value">The value to be stored in the cache.</param>
    /// <param name="expiration">The duration for which the cached value is valid before it expires.</param>
    public static void Store<T>(string cacheKey, T value, TimeSpan expiration)
    {
        MemoryCache.Set(cacheKey, value!, expiration);
    }

    /// <summary>
    ///     Attempts to retrieve a value from the in-memory cache associated with the specified cache key.
    /// </summary>
    /// <typeparam name="T">The type of the value to be retrieved from the cache.</typeparam>
    /// <param name="cacheKey">The unique key used to retrieve the cached value.</param>
    /// <param name="value">
    ///     When this method returns, contains the value associated with the specified cache key,
    ///     if the key exists and the value is of the specified type. Otherwise, the value is default.
    /// </param>
    /// <returns>
    ///     True if the retrieval was successful and the key exists in the cache with a matching value type; otherwise, false.
    /// </returns>
    public static bool TryGet<T>(string cacheKey, out T? value)
    {
        if (MemoryCache.TryGetValue(cacheKey, out var cached) && cached is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }
}