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
    private static bool _useAspireIntegration;
    private static IServiceProvider? _serviceProvider;
    private static readonly ICaeriusLogger? Logger = LoggerProvider.GetLogger();

    /// <summary>
    ///     Configures the Redis cache manager to use Aspire integration
    /// </summary>
    internal static void UseAspireIntegration()
    {
        _useAspireIntegration = true;
        _isInitialized = true;
        Logger?.LogInformation(LogCategory.Redis, "Redis cache manager configured to use Aspire integration.");
    }

    /// <summary>
    ///     Sets the service provider for dependency injection
    /// </summary>
    /// <param name="serviceProvider">The service provider instance</param>
    internal static void SetServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

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
    ///     Gets the Redis database from either direct connection or Aspire integration
    /// </summary>
    /// <returns>The Redis database instance or null if not available</returns>
    private static IDatabase? GetDatabase()
    {
        if (!_useAspireIntegration || _serviceProvider == null) return _database;
        try
        {
            var redisCacheConnection = _serviceProvider.GetRequiredService<IRedisCacheConnection>();
            return redisCacheConnection.GetDatabase();
        }
        catch (Exception ex)
        {
            Logger?.LogError(LogCategory.Redis, "Failed to get Redis database from service provider", ex);
            return null;
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
        var database = GetDatabase();
        if (!_isInitialized || database == null)
        {
            Logger?.LogWarning(LogCategory.Redis,
                $"Attempt to store in Redis with key '{cacheKey}' while Redis is not initialized.");
            return;
        }

        try
        {
            Logger?.LogDebug(LogCategory.Redis, $"Storing in Redis with key '{cacheKey}'...");
            var serialized = JsonSerializer.Serialize(value);
            var result = database.StringSet(cacheKey, serialized, expiration);

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
        var database = GetDatabase();
        if (!_isInitialized || database == null)
        {
            Logger?.LogWarning(LogCategory.Redis,
                $"Attempt to retrieve from Redis with key '{cacheKey}' while Redis is not initialized.");
            return false;
        }

        try
        {
            Logger?.LogDebug(LogCategory.Redis, $"Retrieving from Redis with key '{cacheKey}'...");
            var cached = database.StringGet(cacheKey);

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