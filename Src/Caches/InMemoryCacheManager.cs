namespace CaeriusNet.Caches;

/// <summary>
///     Represents a utility class that manages caching operations in memory,
///     such as storing and retrieving cached data.
/// </summary>
/// <remarks>
///     This class provides functionality for in-memory caching using
///     <see cref="Microsoft.Extensions.Caching.Memory.MemoryCache" />.
/// </remarks>
static internal class InMemoryCacheManager
{
	/// <summary>
	///     The memory cache instance used for storing cached items.
	/// </summary>
	private static readonly MemoryCache MemoryCache = new(new MemoryCacheOptions
	{
		SizeLimit = null,
		CompactionPercentage = 0.05,
		ExpirationScanFrequency = TimeSpan.FromMinutes(2),
		TrackLinkedCacheEntries = false
	});

	/// <summary>
	///     The logger instance used for recording cache operations.
	/// </summary>
	private static readonly ILogger? Logger = LoggerProvider.GetLogger();

	/// <summary>
	///     Indicates whether logging is enabled.
	/// </summary>
	private static readonly bool IsLoggingEnabled = Logger != null;

	/// <summary>
	///     Stores the specified value in the in-memory cache with the given cache key and expiration time.
	/// </summary>
	/// <typeparam name="T">The type of the value to be stored in the cache.</typeparam>
	/// <param name="cacheKey">The unique key used to store and retrieve the value from the cache.</param>
	/// <param name="value">The value to be stored in the cache.</param>
	/// <param name="expiration">The duration for which the cached value is valid before it expires.</param>
	/// <remarks>
	///     This method will log the caching operation if logging is enabled.
	///     The value is stored in the memory cache with the specified expiration duration.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static internal void Store<T>(string cacheKey, T value, TimeSpan expiration)
	{
		if (IsLoggingEnabled)
			Logger!.LogStoringInMemoryCache(cacheKey);

		var options = new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = expiration,
			Priority = CacheItemPriority.Normal
		};

		MemoryCache.Set(cacheKey, value!, options);
	}

	/// <summary>
	///     Attempts to retrieve a cached value from the in-memory cache based on the specified cache key.
	/// </summary>
	/// <typeparam name="T">The type of value expected to be retrieved from the cache.</typeparam>
	/// <param name="cacheKey">The unique identifier for the cached item.</param>
	/// <param name="value">
	///     When this method returns, contains the retrieved value if the key is found and the value is of type
	///     <typeparamref name="T" />;
	///     otherwise, the default value for type <typeparamref name="T" />.
	/// </param>
	/// <returns>
	///     <see langword="true" /> if the cache contains an item with the specified key and the value is of type
	///     <typeparamref name="T" />;
	///     otherwise, <see langword="false" />.
	/// </returns>
	/// <remarks>
	///     This method will log the retrieval operation if logging is enabled and the value is successfully retrieved.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static internal bool TryGet<T>(string cacheKey, out T? value)
	{
		if (!MemoryCache.TryGetValue(cacheKey, out object? cached) || cached is not T typedValue){
			value = default;
			return false;
		}

		value = typedValue;

		if (IsLoggingEnabled)
			Logger!.LogRetrievedFromMemoryCache(cacheKey);

		return true;
	}
}