using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace CaeriusNet.Caches;

/// <summary>
///     Manages a thread-safe immutable cache utilizing frozen dictionaries for optimized read access.
/// </summary>
public static class FrozenCacheManager
{
    private static volatile FrozenDictionary<string, object> _frozenCache = FrozenDictionary<string, object>.Empty;
    private static readonly object Lock = new();

    /// <summary>
    ///     Stores a value in the frozen dictionary-based cache if it is not already present.
    /// </summary>
    /// <typeparam name="T">The type of the value to be stored in the cache.</typeparam>
    /// <param name="cacheKey">The unique key to associate with the value in the cache.</param>
    /// <param name="value">The value to be stored in the cache.</param>
    public static void StoreFrozen<T>(string cacheKey, T value)
    {
        lock (Lock)
        {
            if (_frozenCache.ContainsKey(cacheKey)) return;

            var mutableCache = new ConcurrentDictionary<string, object>(_frozenCache)
            {
                [cacheKey] = value!
            };
            _frozenCache = mutableCache.ToFrozenDictionary();
        }
    }

    /// <summary>
    ///     Attempts to retrieve a value from the frozen dictionary-based cache.
    /// </summary>
    /// <typeparam name="T">The expected type of the cached value.</typeparam>
    /// <param name="cacheKey">The unique key associated with the value in the cache.</param>
    /// <param name="value">The output parameter where the cached value will be stored if found.</param>
    /// <returns>
    ///     <c>true</c> if the value is found in the cache and its type matches the specified type <typeparamref name="T" />;
    ///     otherwise, <c>false</c>.
    /// </returns>
    public static bool TryGetFrozen<T>(string cacheKey, out T? value)
    {
        if (_frozenCache.TryGetValue(cacheKey, out var cached) && cached is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }
}