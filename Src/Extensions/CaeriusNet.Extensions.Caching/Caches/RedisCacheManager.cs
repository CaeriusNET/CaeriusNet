using CaeriusNet.Extensions.Caching.Factories;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Text.Json;

namespace CaeriusNet.Extensions.Caching.Caches;

/// <summary>
///     Provides methods to manage Redis-based distributed cache.
/// </summary>
public static class RedisCacheManager
{
	private static ConnectionMultiplexer? _connection;
	private static IDatabase? _database;
	private static bool _isInitialized;
	private static bool _useAspireIntegration;
	private static IServiceProvider? _serviceProvider;

	/// <summary>
	///     Configures the Redis cache manager to use Aspire integration
	/// </summary>
	public static void UseAspireIntegration()
	{
		_useAspireIntegration = true;
		_isInitialized = true;
	}

	/// <summary>
	///     Sets the service provider for dependency injection
	/// </summary>
	/// <param name="serviceProvider">The service provider instance</param>
	static internal void SetServiceProvider(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	/// <summary>
	///     Initializes the Redis cache manager with the provided connection string.
	/// </summary>
	/// <param name="connectionString">The connection string to the Redis server.</param>
	/// <returns>True if initialization succeeded, otherwise False.</returns>
	static internal void Initialize(string connectionString)
	{
		if (_isInitialized)
			return;

		try{
			_connection = ConnectionMultiplexer.Connect(connectionString);
			_database = _connection.GetDatabase();
			_isInitialized = true;
		}
		catch (Exception ex){
			_connection?.Dispose();
			_connection = null;
			_database = null;
			_isInitialized = false;
		}
	}

	/// <summary>
	///     Gets the Redis database from either direct connection or Aspire integration
	/// </summary>
	/// <returns>The Redis database instance or null if not available</returns>
	private static IDatabase? GetDatabase()
	{
		if (!_useAspireIntegration || _serviceProvider == null) return _database;

		try{
			var redisCacheConnection = _serviceProvider.GetRequiredService<IRedisCacheConnection>();
			return redisCacheConnection.GetDatabase();
		}
		catch (Exception ex){ return null; }
	}

	/// <summary>
	///     Checks if the Redis cache manager is initialized.
	/// </summary>
	/// <returns>True if the manager is initialized, otherwise False.</returns>
	static internal bool IsInitialized()
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
	static internal void Store<T>(string cacheKey, T value, TimeSpan? expiration)
	{
		var database = GetDatabase();
		if (!_isInitialized || database == null)
			return;

		try{
			string serialized = JsonSerializer.Serialize(value);
			bool result = database.StringSet(cacheKey, serialized, expiration);
		}
		catch (Exception ex){}
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
	static internal bool TryGet<T>(string cacheKey, out T? value)
	{
		value = default;
		var database = GetDatabase();
		if (!_isInitialized || database == null)
			return false;

		try{
			var cached = database.StringGet(cacheKey);

			if (cached.IsNull)
				return false;

			value = JsonSerializer.Deserialize<T>(cached!);
			bool success = value != null;
			return success;
		}
		catch (Exception ex){ return false; }
	}
}