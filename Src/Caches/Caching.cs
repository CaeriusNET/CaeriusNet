using System.Collections.Concurrent;
using System.Collections.Frozen;
using Microsoft.Extensions.Caching.Memory;

namespace CaeriusNet.Caches;

public sealed class Caching
{
    private static readonly ConcurrentDictionary<string, object> FrozenCache = new();

    public static IReadOnlyDictionary<TKey, TValue> GetOrAdd<TKey, TValue>(
        string cacheKey,
        Func<IReadOnlyDictionary<TKey, TValue>> dataRetriever)
        where TKey : notnull
    {
        if (FrozenCache.TryGetValue(cacheKey, out var cached))
            return (IReadOnlyDictionary<TKey, TValue>)cached;

        var data = dataRetriever();
        var frozenDictionary = data.ToFrozenDictionary(x => x.Key, x => x.Value);
        FrozenCache[cacheKey] = frozenDictionary;
        return frozenDictionary;
    }

    public static class InMemoryCacheManager
    {
        private static readonly MemoryCache MemoryCache = new(new MemoryCacheOptions());

        public static IReadOnlyDictionary<TKey, TValue> GetOrAdd<TKey, TValue>(
            string cacheKey,
            Func<IReadOnlyDictionary<TKey, TValue>> dataRetriever,
            TimeSpan expiration)
        {
            if (MemoryCache.TryGetValue(cacheKey, out var cached))
                return (IReadOnlyDictionary<TKey, TValue>)cached;

            var data = dataRetriever();
            var cacheEntry = MemoryCache.Set(cacheKey, data, expiration);
            return cacheEntry;
        }
    }
}