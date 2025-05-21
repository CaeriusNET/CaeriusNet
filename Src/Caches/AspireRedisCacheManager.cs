using CaeriusNet.Logging;
using Microsoft.Extensions.Caching.Distributed;

namespace CaeriusNet.Caches;

/// <summary>
///     Provides methods to manage Redis-based distributed cache using .NET Aspire integration.
/// </summary>
internal sealed class AspireRedisCacheManager
{
	private static AspireRedisCacheManager? _instance;
	private static readonly ICaeriusLogger? Logger = LoggerProvider.GetLogger();
	private readonly IDistributedCache _distributedCache;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AspireRedisCacheManager" /> class.
    /// </summary>
    /// <param name="distributedCache">The distributed cache instance.</param>
    public AspireRedisCacheManager(IDistributedCache distributedCache)
	{
		_distributedCache = distributedCache;
		_instance = this;
		Logger?.LogInformation(LogCategory.Redis, "AspireRedisDistribuedCacheManager initialized successfully.");
	}

    /// <summary>
    ///     Checks if the AspireRedisCacheManager is initialized.
    /// </summary>
    /// <returns>True if the manager is initialized, otherwise False.</returns>
    internal static bool IsInitialized()
	{
		return _instance != null;
	}

    /// <summary>
    ///     Stores a value in the Redis distributed cache.
    /// </summary>
    /// <typeparam name="T">The type of value to store in the cache.</typeparam>
    /// <param name="cacheKey">The unique key to identify the value in the cache.</param>
    /// <param name="value">The value to store in the cache.</param>
    /// <param name="expiration">The validity duration of the value in the cache before expiration.</param>
    internal static void Store<T>(string cacheKey, T value, TimeSpan? expiration)
	{
		if (_instance == null)
		{
			Logger?.LogWarning(LogCategory.Redis,
				$"Attempt to store in Aspire Redis with key '{cacheKey}' while the manager is not initialized.");
			return;
		}

		try
		{
			Logger?.LogDebug(LogCategory.Redis, $"Storing in Aspire Redis with key '{cacheKey}'...");
			var serialized = JsonSerializer.Serialize(value);

			var options = new DistributedCacheEntryOptions();
			if (expiration.HasValue) options.AbsoluteExpirationRelativeToNow = expiration;

			_instance._distributedCache.SetString(cacheKey, serialized, options);

			Logger?.LogInformation(LogCategory.Redis,
				$"Value stored in Aspire Redis with key '{cacheKey}' and expiration {(expiration.HasValue ? expiration.Value.ToString() : "unlimited")}");
		}
		catch (Exception ex)
		{
			Logger?.LogError(LogCategory.Redis, $"Error while storing in Aspire Redis with key '{cacheKey}'", ex);
		}
	}

    /// <summary>
    ///     Attempts to retrieve a cached value from Redis distributed cache.
    /// </summary>
    /// <typeparam name="T">The expected type of the cached value.</typeparam>
    /// <param name="cacheKey">The unique key associated with the value in the cache.</param>
    /// <param name="value">The output parameter where the cached value will be stored if found.</param>
    /// <returns>
    ///     True if the value is found in the cache and its type matches the specified type <typeparamref name="T" />;
    ///     otherwise, False.
    /// </returns>
    internal static bool TryGet<T>(string cacheKey, out T? value)
	{
		value = default;
		if (_instance == null)
		{
			Logger?.LogWarning(LogCategory.Redis,
				$"Attempt to retrieve from Aspire Redis with key '{cacheKey}' while the manager is not initialized.");
			return false;
		}

		try
		{
			Logger?.LogDebug(LogCategory.Redis, $"Retrieving from Aspire Redis with key '{cacheKey}'...");
			var cached = _instance._distributedCache.GetString(cacheKey);

			if (cached == null)
			{
				Logger?.LogDebug(LogCategory.Redis, $"No value found in Aspire Redis for key '{cacheKey}'");
				return false;
			}

			value = JsonSerializer.Deserialize<T>(cached);
			var success = value != null;

			if (success)
				Logger?.LogInformation(LogCategory.Redis,
					$"Value successfully retrieved from Aspire Redis for key '{cacheKey}'");
			else
				Logger?.LogWarning(LogCategory.Redis, $"Deserialization failed for key '{cacheKey}'");

			return success;
		}
		catch (Exception ex)
		{
			Logger?.LogError(LogCategory.Redis, $"Error while retrieving from Aspire Redis with key '{cacheKey}'", ex);
			return false;
		}
	}
}