namespace CaeriusNet.Caches;

/// <summary>
///     Provides methods for managing an immutable, thread-safe cache using frozen dictionaries.
/// </summary>
static internal class FrozenCacheManager
{
	/// <summary>
	///     The frozen dictionary that serves as the cache storage.
	/// </summary>
	private static FrozenDictionary<string, object> _frozenCache = FrozenDictionary<string, object>.Empty;

	private static SpinLock _spinLock = new(enableThreadOwnerTracking: false);

	/// <summary>
	///     Lock object used for thread synchronization when modifying the cache.
	/// </summary>
	private static readonly Lock Lock = new();

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
	static internal void Store<T>(string cacheKey, T value)
	{
		var currentCache = Volatile.Read(ref _frozenCache);
		if (currentCache.ContainsKey(cacheKey))
			return;

		bool lockTaken = false;
		try{
			_spinLock.Enter(ref lockTaken);

			currentCache = Volatile.Read(ref _frozenCache);
			if (currentCache.ContainsKey(cacheKey)) return;

			var builder = currentCache.Count == 0
				? new Dictionary<string, object>(1, StringComparer.Ordinal)
				: new Dictionary<string, object>(currentCache.Count + 1, StringComparer.Ordinal);

			foreach (var kvp in currentCache)
				builder[kvp.Key] = kvp.Value;

			builder[cacheKey] = value!;

			Volatile.Write(ref _frozenCache, builder.ToFrozenDictionary(StringComparer.Ordinal));

			if (IsLoggingEnabled)
				Logger!.LogStoredInFrozenCache(cacheKey);
		}
		finally{
			if (lockTaken)
				_spinLock.Exit();
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
	static internal bool TryGet<T>(string cacheKey, out T? value)
	{
		var cache = Volatile.Read(ref _frozenCache);

		if (!cache.TryGetValue(cacheKey, out object? cached) || cached is not T typedValue){
			value = default;
			return false;
		}

		value = typedValue;

		if (IsLoggingEnabled)
			Logger!.LogRetrievedFromFrozenCache(cacheKey);

		return true;
	}
}