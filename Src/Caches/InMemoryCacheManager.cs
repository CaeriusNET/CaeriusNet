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
    private const int RetiredStateFlag = unchecked((int)0x80000000);
    private const int ReferenceCountMask = 0x7fffffff;

    /// <summary>
    ///     The memory cache state used for storing cached items. Replaceable via <see cref="Configure" />
    ///     before any Store/TryGet call (typically from the DI builder at startup).
    /// </summary>
    private static CacheState _state = CreateDefaultState();

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

        var next = new CacheState(new MemoryCache(options), options.SizeLimit.HasValue);
        var previous = Interlocked.Exchange(ref _state, next);
        previous.Retire();
    }

    /// <summary>
    ///     Stores the specified value in the in-memory cache with the given cache key and expiration time.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Store<T>(string cacheKey, T value, TimeSpan expiration)
    {
        if (IsLoggingEnabled)
            Logger!.LogStoringInMemoryCache(cacheKey);

        using var lease = AcquireState();

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration,
            Priority = CacheItemPriority.Normal
        };

        if (lease.SizeEntries)
            options.Size = 1;

        lease.Cache.Set(cacheKey, value!, options);
    }

    /// <summary>
    ///     Attempts to retrieve a cached value from the in-memory cache based on the specified cache key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryGet<T>(string cacheKey, out T? value)
    {
        using var lease = AcquireState();

        if (!lease.Cache.TryGetValue(cacheKey, out var cached) || cached is not T typedValue)
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
        using var lease = AcquireState();
        lease.Cache.Remove(cacheKey);
    }

    /// <summary>
    ///     Evicts every entry currently in the cache. Subsequent reads will miss until repopulated.
    /// </summary>
    internal static void Clear()
    {
        using var lease = AcquireState();
        lease.Cache.Clear();
    }

    private static CacheState CreateDefaultState()
    {
        return new CacheState(new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = null,
            CompactionPercentage = 0.05,
            ExpirationScanFrequency = TimeSpan.FromMinutes(2),
            TrackLinkedCacheEntries = false
        }), false);
    }

    private static CacheLease AcquireState()
    {
        while (true)
        {
            var state = Volatile.Read(ref _state);
            if (state.TryAddReference())
                return new CacheLease(state);
        }
    }

    private sealed class CacheState(MemoryCache cache, bool sizeEntries)
    {
        private int _referenceState;

        internal MemoryCache Cache { get; } = cache;

        internal bool SizeEntries { get; } = sizeEntries;

        internal bool TryAddReference()
        {
            while (true)
            {
                var current = Volatile.Read(ref _referenceState);
                if ((current & RetiredStateFlag) != 0)
                    return false;

                var referenceCount = current & ReferenceCountMask;
                if (referenceCount == ReferenceCountMask)
                    throw new InvalidOperationException("The in-memory cache has too many concurrent operations.");

                if (Interlocked.CompareExchange(ref _referenceState, current + 1, current) == current)
                    return true;
            }
        }

        internal void Release()
        {
            var current = Interlocked.Decrement(ref _referenceState);
            if ((current & RetiredStateFlag) != 0 && (current & ReferenceCountMask) == 0)
                Cache.Dispose();
        }

        internal void Retire()
        {
            while (true)
            {
                var current = Volatile.Read(ref _referenceState);
                if ((current & RetiredStateFlag) != 0)
                    return;

                var retired = current | RetiredStateFlag;
                if (Interlocked.CompareExchange(ref _referenceState, retired, current) != current)
                    continue;

                if ((retired & ReferenceCountMask) == 0)
                    Cache.Dispose();

                return;
            }
        }
    }

    private readonly struct CacheLease(CacheState state) : IDisposable
    {
        internal MemoryCache Cache => state.Cache;

        internal bool SizeEntries => state.SizeEntries;

        public void Dispose()
        {
            state.Release();
        }
    }
}
