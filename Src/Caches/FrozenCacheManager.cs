using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace CaeriusNet.Caches;

public static class FrozenCacheManager
{
    private static volatile FrozenDictionary<string, object> _frozenCache = FrozenDictionary<string, object>.Empty;
    private static readonly object Lock = new();

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