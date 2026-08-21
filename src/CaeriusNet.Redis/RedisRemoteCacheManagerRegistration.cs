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

    // These members must be forwarded explicitly. Default interface implementations are selected from the
    // ICaeriusRemoteCacheManager static type used by the runtime and would otherwise silently downgrade Redis
    // dependency tags to key removals. That loses the atomic generation-snapshot protocol implemented by
    // RedisCacheManager and allows a slow read-through to republish a value after an invalidation.
    public ValueTask<CaeriusCacheTagSnapshot> CaptureTagSnapshotAsync(
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default)
    {
        return manager.CaptureTagSnapshotAsync(tags, cancellationToken);
    }

    public ValueTask<CaeriusCacheLookup<T>> TryGetAsync<T>(
        string key,
        IReadOnlyList<string> tags,
        ICaeriusCacheCodec<T> codec,
        CancellationToken cancellationToken = default)
    {
        return manager.TryGetAsync(key, tags, codec, cancellationToken);
    }

    public ValueTask<bool> SetIfTagSnapshotUnchangedAsync<T>(
        string key,
        T value,
        IReadOnlyList<string> tags,
        CaeriusCacheTagSnapshot snapshot,
        ICaeriusCacheCodec<T> codec,
        TimeSpan? expiration,
        CancellationToken cancellationToken = default) where T : notnull
    {
        return manager.SetIfTagSnapshotUnchangedAsync(
            key,
            value,
            tags,
            snapshot,
            codec,
            expiration,
            cancellationToken);
    }

    public ValueTask InvalidateTagsAsync(
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default)
    {
        return manager.InvalidateTagsAsync(tags, cancellationToken);
    }
}
