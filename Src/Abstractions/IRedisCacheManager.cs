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
    ///     Stores a value in the cache.
    /// </summary>
    void Store<T>(string cacheKey, T value, TimeSpan? expiration) where T : notnull;
}