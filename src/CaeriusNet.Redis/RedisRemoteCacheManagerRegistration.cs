using CaeriusNet.Abstractions;

namespace CaeriusNet.Redis;

internal sealed class RedisRemoteCacheManagerRegistration(IRedisCacheManager manager) : ICaeriusRemoteCacheManager
{
    public ValueTask<CaeriusCacheLookup<T>> TryGetAsync<T>(
        string key,
        ICaeriusCacheCodec<T> codec,
        CancellationToken cancellationToken = default)
    {
        return manager.TryGetAsync(key, codec, cancellationToken);
    }

    public ValueTask SetAsync<T>(
        string key,
        T value,
        ICaeriusCacheCodec<T> codec,
        TimeSpan? expiration,
        CancellationToken cancellationToken = default) where T : notnull
    {
        return manager.SetAsync(key, value, codec, expiration, cancellationToken);
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return manager.RemoveAsync(key, cancellationToken);
    }
}