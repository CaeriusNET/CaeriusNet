using CaeriusNet.Logging;

namespace CaeriusNet.Caches;

/// <summary>
///     Provides methods to manage Redis-based distributed cache.
/// </summary>
internal static class RedisCacheManager
{
	private static ConnectionMultiplexer? _connection;
	private static IDatabase? _database;
	private static bool _isInitialized;
	private static readonly ICaeriusLogger? Logger = LoggerProvider.GetLogger();

	/// <summary>
	///     Initializes the Redis cache manager with the provided connection string.
	/// </summary>
	/// <param name="connectionString">The connection string to the Redis server.</param>
	/// <returns>True if initialization succeeded, otherwise False.</returns>
	internal static void Initialize(string connectionString)
	{
		if (_isInitialized)
		{
			Logger?.LogDebug(LogCategory.Redis, "Redis manager is already initialized. Ignoring reinitialization.");
			return;
		}

		Logger?.LogDebug(LogCategory.Redis, "Attempting to connect to Redis server...");

		try
		{
			_connection = ConnectionMultiplexer.Connect(connectionString);
			_database = _connection.GetDatabase();
			_isInitialized = true;
			Logger?.LogInformation(LogCategory.Redis, "Redis server connection established successfully.");
		}
		catch (Exception ex)
		{
			Logger?.LogError(LogCategory.Redis, "Failed to connect to Redis server", ex);
			_connection?.Dispose();
			_connection = null;
			_database = null;
			_isInitialized = false;
		}
	}

	/// <summary>
	///     Checks if the Redis cache manager is initialized.
	/// </summary>
	/// <returns>True if the manager is initialized, otherwise False.</returns>
	internal static bool IsInitialized()
	{
		return _isInitialized;
	}

	/// <summary>
	///     Stores a value in the Redis cache.
	/// </summary>
	/// <typeparam name="T">The type of value to store in the cache.</typeparam>
	/// <param name="cacheKey">The unique key to identify the value in the cache.</param>
	/// <param name="value">The value to store in the cache.</param>
	/// <param name="expiration">The validity duration of the value in the cache before expiration.</param>
	/// <returns>True if storage was successful, otherwise False.</returns>
	internal static void Store<T>(string cacheKey, T value, TimeSpan? expiration)
	{
		if (!_isInitialized || _database == null)
		{
			Logger?.LogWarning(LogCategory.Redis,
				$"Attempt to store in Redis with key '{cacheKey}' while Redis is not initialized.");
			return;
		}

		try
		{
			Logger?.LogDebug(LogCategory.Redis, $"Storing in Redis with key '{cacheKey}'...");
			var serialized = JsonSerializer.Serialize(value);
			var result = _database.StringSet(cacheKey, serialized, expiration);

			if (result)
				Logger?.LogInformation(LogCategory.Redis,
					$"Value stored in Redis with key '{cacheKey}' and expiration {(expiration.HasValue ? expiration.Value.ToString() : "unlimited")}");
			else
				Logger?.LogWarning(LogCategory.Redis, $"Failed to store value in Redis with key '{cacheKey}'");
		}
		catch (Exception ex)
		{
			Logger?.LogError(LogCategory.Redis, $"Error while storing in Redis with key '{cacheKey}'", ex);
		}
	}

	/// <summary>
	///     Attempts to retrieve a cached value from Redis.
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
		if (!_isInitialized || _database == null)
		{
			Logger?.LogWarning(LogCategory.Redis,
				$"Attempt to retrieve from Redis with key '{cacheKey}' while Redis is not initialized.");
			return false;
		}

		try
		{
			Logger?.LogDebug(LogCategory.Redis, $"Retrieving from Redis with key '{cacheKey}'...");
			var cached = _database.StringGet(cacheKey);

			if (cached.IsNull)
			{
				Logger?.LogDebug(LogCategory.Redis, $"No value found in Redis for key '{cacheKey}'");
				return false;
			}

			value = JsonSerializer.Deserialize<T>(cached!);
			var success = value != null;

			if (success)
				Logger?.LogInformation(LogCategory.Redis,
					$"Value successfully retrieved from Redis for key '{cacheKey}'");
			else
				Logger?.LogWarning(LogCategory.Redis, $"Deserialization failed for key '{cacheKey}'");

			return success;
		}
		catch (Exception ex)
		{
			Logger?.LogError(LogCategory.Redis, $"Error while retrieving from Redis with key '{cacheKey}'", ex);
			return false;
		}
	}
}