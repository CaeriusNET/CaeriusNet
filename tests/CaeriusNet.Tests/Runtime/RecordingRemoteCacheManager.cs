using CaeriusNet.Abstractions;

namespace CaeriusNet.Tests.Runtime;

internal sealed class RecordingRemoteCacheManager : ICaeriusRemoteCacheManager
{
    public List<string> RemovedKeys { get; } = [];
    public List<string> LookupKeys { get; } = [];
    public List<string> SetKeys { get; } = [];

    public ValueTask<CaeriusCacheLookup<T>> TryGetAsync<T>(
        string key,
        ICaeriusCacheCodec<T> codec,
        CancellationToken cancellationToken = default)
    {
        LookupKeys.Add(key);
        return ValueTask.FromResult(CaeriusCacheLookup<T>.Miss);
    }

    public ValueTask SetAsync<T>(
        string key,
        T value,
        ICaeriusCacheCodec<T> codec,
        TimeSpan? expiration,
        CancellationToken cancellationToken = default)
        where T : notnull
    {
        SetKeys.Add(key);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        RemovedKeys.Add(key);
        return ValueTask.CompletedTask;
    }
}