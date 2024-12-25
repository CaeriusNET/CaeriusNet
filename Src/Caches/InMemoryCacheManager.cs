using Microsoft.Extensions.Caching.Memory;

namespace CaeriusNet.Caches;

public static class InMemoryCacheManager
{
    private static readonly MemoryCache MemoryCache = new(new MemoryCacheOptions());

    public static void Store<T>(string cacheKey, T value, TimeSpan expiration)
    {
        MemoryCache.Set(cacheKey, value!, expiration);
    }

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