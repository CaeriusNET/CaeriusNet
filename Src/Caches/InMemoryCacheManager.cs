namespace CaeriusNet.Caches;

/// <summary>
///     Represents a utility class that manages caching operations in memory,
///     such as storing and retrieving cached data.
/// </summary>
/// <remarks>
///     This class provides functionality for in-memory caching using
///     <see cref="Microsoft.Extensions.Caching.Memory.MemoryCache" />.
/// </remarks>
internal static class InMemoryCacheManager
{
    /// <summary>
    ///     The memory cache instance used for storing cached items. Replaceable via <see cref="Configure" />
    ///     before any Store/TryGet call (typically from the DI builder at startup).
    /// </summary>
    private static MemoryCache _memoryCache = new(new MemoryCacheOptions
    {
        SizeLimit = null,
        CompactionPercentage = 0.05,
        ExpirationScanFrequency = TimeSpan.FromMinutes(2),
        TrackLinkedCacheEntries = false
    });

    /// <summary>
    ///     When non-null, every cache entry is sized as 1 so that <see cref="MemoryCacheOptions.SizeLimit" />
    ///     effectively caps the maximum number of resident entries. Null preserves legacy unbounded behavior.
    /// </summary>
    private static long? _entrySize;

    /// <summary>
    ///     The logger instance used for recording cache operations.
    /// </summary>
    private static readonly ILogger? Logger = LoggerProvider.GetLogger();

    /// <summary>
    ///     Indicates whether logging is enabled.
    /// </summary>
    private static readonly bool IsLoggingEnabled = Logger != null;

    /// <summary>
    ///     Replaces the underlying <see cref="MemoryCache" /> with a freshly configured instance. Any items already
    ///     stored in the previous cache are released. When <see cref="MemoryCacheOptions.SizeLimit" /> is set,
    ///     entries are sized as 1 so the limit acts as a maximum entry count.
    /// </summary>
    /// <param name="options">The new memory cache options. Must not be null.</param>
    internal static void Configure(MemoryCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var previous = Interlocked.Exchange(ref _memoryCache, new MemoryCache(options));
        _entrySize = options.SizeLimit;
        previous.Dispose();
    }

    /// <summary>
    ///     Stores the specified value in the in-memory cache with the given cache key and expiration time.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Store<T>(string cacheKey, T value, TimeSpan expiration)
    {
        if (IsLoggingEnabled)
            Logger!.LogStoringInMemoryCache(cacheKey);

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration,
            Priority = CacheItemPriority.Normal
        };

        if (_entrySize.HasValue)
            options.Size = 1;

        _memoryCache.Set(cacheKey, value!, options);
    }

    /// <summary>
    ///     Attempts to retrieve a cached value from the in-memory cache based on the specified cache key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryGet<T>(string cacheKey, out T? value)
    {
        if (!_memoryCache.TryGetValue(cacheKey, out var cached) || cached is not T typedValue)
        {
            value = default;
            return false;
        }

        value = typedValue;

        if (IsLoggingEnabled)
            Logger!.LogRetrievedFromMemoryCache(cacheKey);

        return true;
    }

    /// <summary>
    ///     Removes the entry associated with the specified key, if present. No-op if the key is unknown.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Remove(string cacheKey)
    {
        _memoryCache.Remove(cacheKey);
    }

    /// <summary>
    ///     Evicts every entry currently in the cache. Subsequent reads will miss until repopulated.
    /// </summary>
    internal static void Clear()
    {
        _memoryCache.Clear();
    }
}
