namespace CaeriusNet.Abstractions;

/// <summary>
///     Provides Redis cache operations with proper dependency injection
/// </summary>
public interface IRedisCacheManager
{
	/// <summary>
	///     Gets a value indicating whether the Redis cache manager is initialized and ready to use
	/// </summary>
	bool IsInitialized { get; }

	/// <summary>
	///     Stores a value in the Redis cache with an optional expiration time
	/// </summary>
	/// <typeparam name="T">The type of value being stored</typeparam>
	/// <param name="cacheKey">The unique key to identify the cached item</param>
	/// <param name="value">The value to store in the cache</param>
	/// <param name="expiration">Optional TimeSpan specifying when the cached item should expire</param>
	void Store<T>(string cacheKey, T value, TimeSpan? expiration);

	/// <summary>
	///     Attempts to retrieve a value from the Redis cache
	/// </summary>
	/// <typeparam name="T">The type of value to retrieve</typeparam>
	/// <param name="cacheKey">The unique key of the cached item</param>
	/// <param name="value">When this method returns, contains the retrieved value if found, or default value if not found</param>
	/// <returns>true if the value was found in the cache; otherwise, false</returns>
	bool TryGet<T>(string cacheKey, out T? value);
}