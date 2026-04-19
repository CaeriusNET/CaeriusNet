using System.Numerics;

namespace CaeriusNet.Caches;

/// <summary>
///     Provides methods for managing an immutable, thread-safe cache using frozen dictionaries.
/// </summary>
internal static class FrozenCacheManager
{
	/// <summary>
	///     The frozen dictionary that serves as the cache storage.
	/// </summary>
	private static FrozenDictionary<string, object> _frozenCache = FrozenDictionary<string, object>.Empty;

	/// <summary>
	///     A thread synchronization lock that manages access to the frozen cache.
	///     Uses no recursion policy to prevent potential deadlocks.
	/// </summary>
	private static readonly ReaderWriterLockSlim LockSlim = new(LockRecursionPolicy.NoRecursion);

	/// <summary>
	///     Logger instance for cache operations.
	/// </summary>
	private static readonly ILogger? Logger = LoggerProvider.GetLogger();

	/// <summary>
	///     Flag indicating whether logging is enabled.
	/// </summary>
	private static readonly bool IsLoggingEnabled = Logger != null;

	/// <summary>
	///     Stores a value in the frozen dictionary-based cache if it is not already present.
	/// </summary>
	/// <typeparam name="T">The type of the value to be stored in the cache.</typeparam>
	/// <param name="cacheKey">The unique key to associate with the value in the cache.</param>
	/// <param name="value">The value to be stored in the cache.</param>
	/// <remarks>
	///     This method is thread-safe and uses double-checked locking pattern for thread synchronization.
	///     The cache is immutable and a new frozen dictionary is created when adding new items.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static void Store<T>(string cacheKey, T value)
    {
        LockSlim.EnterReadLock();
        try
        {
            if (_frozenCache.ContainsKey(cacheKey))
                return;
        }
        finally
        {
            LockSlim.ExitReadLock();
        }

        LockSlim.EnterWriteLock();
        try
        {
            if (_frozenCache.ContainsKey(cacheKey))
                return;

            var currentCache = _frozenCache;

            var newCapacity = currentCache.Count == 0
                ? 16
                : (int)BitOperations.RoundUpToPowerOf2((uint)(currentCache.Count + 1));

            var builder = new Dictionary<string, object>(newCapacity, StringComparer.Ordinal);

            foreach (var kvp in currentCache)
                builder[kvp.Key] = kvp.Value;

            builder[cacheKey] = value!;

            _frozenCache = builder.ToFrozenDictionary(StringComparer.Ordinal);

            if (IsLoggingEnabled)
                Logger!.LogStoredInFrozenCache(cacheKey);
        }
        finally
        {
            LockSlim.ExitWriteLock();
        }
    }

	/// <summary>
	///     Stores multiple values in the frozen dictionary-based cache in a single atomic rebuild.
	///     Prefer this over calling <see cref="Store{T}" /> in a loop: it freezes the dictionary only once,
	///     avoiding the O(n²) rebuild cost of sequential single-item inserts.
	/// </summary>
	/// <typeparam name="T">The type of the values to be stored.</typeparam>
	/// <param name="entries">The key-value pairs to add. Keys already present are overwritten.</param>
	/// <remarks>
	///     This method is intended for startup warm-up. <see cref="FrozenDictionary{TKey,TValue}" /> is
	///     designed for "populate once, read many" scenarios; bulk insertion at startup is the ideal usage.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static void StoreRange<T>(IEnumerable<KeyValuePair<string, T>> entries)
    {
        LockSlim.EnterWriteLock();
        try
        {
            var currentCache = _frozenCache;
            var newEntries = entries is ICollection<KeyValuePair<string, T>> col
                ? col
                : entries.ToList();

            if (newEntries.Count == 0) return;

            var newCapacity = (int)BitOperations.RoundUpToPowerOf2((uint)(currentCache.Count + newEntries.Count));
            var builder = new Dictionary<string, object>(newCapacity, StringComparer.Ordinal);

            foreach (var kvp in currentCache)
                builder[kvp.Key] = kvp.Value;

            foreach (var kvp in newEntries)
                builder[kvp.Key] = kvp.Value!;

            _frozenCache = builder.ToFrozenDictionary(StringComparer.Ordinal);

            if (IsLoggingEnabled)
                Logger!.LogStoredRangeInFrozenCache(newEntries.Count);
        }
        finally
        {
            LockSlim.ExitWriteLock();
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
	/// <remarks>
	///     This method is thread-safe and optimized for fast lookups using volatile reads.
	///     It performs type checking to ensure type safety when retrieving cached values.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static bool TryGet<T>(string cacheKey, out T? value)
    {
        LockSlim.EnterReadLock();
        try
        {
            var cache = _frozenCache;

            if (!cache.TryGetValue(cacheKey, out var cached) || cached is not T typedValue)
            {
                value = default;
                return false;
            }

            value = typedValue;

            if (IsLoggingEnabled)
                Logger!.LogRetrievedFromFrozenCache(cacheKey);

            return true;
        }
        finally
        {
            LockSlim.ExitReadLock();
        }
    }
}