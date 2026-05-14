namespace CaeriusNet.Abstractions;

/// <summary>
///     Provides caching operations with support for multiple cache backends.
/// </summary>
public interface IRedisCacheManager
{
    /// <summary>
    ///     Attempts to retrieve a cached value.
    /// </summary>
    bool TryGet<T>(string cacheKey, out T? value);

    /// <summary>
    ///     Attempts to retrieve a cached value using source-generated JSON metadata.
    /// </summary>
    bool TryGet<T>(string cacheKey, JsonTypeInfo<T> jsonTypeInfo, out T? value)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        return TryGet(cacheKey, out value);
    }

    /// <summary>
    ///     Stores a value in the cache.
    /// </summary>
    void Store<T>(string cacheKey, T value, TimeSpan? expiration) where T : notnull;

    /// <summary>
    ///     Stores a value using source-generated JSON metadata.
    /// </summary>
    void Store<T>(string cacheKey, T value, TimeSpan? expiration, JsonTypeInfo<T> jsonTypeInfo) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        Store(cacheKey, value, expiration);
    }

    /// <summary>
    ///     Removes the entry associated with the specified key. No-op if Redis is not configured
    ///     or the key does not exist.
    /// </summary>
    void Remove(string cacheKey);
}
