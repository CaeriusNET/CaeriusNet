using CaeriusNet.Caches;

namespace CaeriusNet.Extensions;

public static class CaeriusCacheExtensions
{
    public static async Task<ReadOnlyCollection<TResultSet>> WithFrozenCaching<TResultSet>(
        this Task<ReadOnlyCollection<TResultSet>> task, string cacheKey)
        where TResultSet : class
    {
        return Caching.GetOrAdd(cacheKey, () =>
        {
            var result = task.GetAwaiter().GetResult();
            return result.ToDictionary(x => x, x => x);
        }).Values.ToList().AsReadOnly();
    }

    public static async Task<ReadOnlyCollection<TResultSet>> WithInMemoryCaching<TResultSet>(
        this Task<ReadOnlyCollection<TResultSet>> task, string cacheKey, TimeSpan expiration)
        where TResultSet : class
    {
        return Caching.InMemoryCacheManager.GetOrAdd(cacheKey, () =>
        {
            var result = task.GetAwaiter().GetResult();
            return result.ToDictionary(x => x, x => x);
        }, expiration).Values.ToList().AsReadOnly();
    }
}