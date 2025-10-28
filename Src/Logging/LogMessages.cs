using System.CodeDom.Compiler;

namespace CaeriusNet.Logging;

/// <summary>
///     High-performance logging messages using source generators for zero-allocation logging.
///     Event IDs are organized by category:
///     - 1xxx: In-Memory Cache
///     - 2xxx: Frozen Cache
///     - 3xxx: Redis Cache
///     - 4xxx: Database Operations
/// </summary>
[GeneratedCode("Microsoft.Extensions.Logging.Generators", "8.0.0.0")]// Type documentation
public static partial class LogMessages
{

	#region In-Memory Cache (1xxx)

	/// <summary>
	///     Logs when storing an item in memory cache
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key being stored</param>
	[LoggerMessage(
	EventId = 1001,
	Level = LogLevel.Debug,
	Message = "Storing in memory cache with key '{cacheKey}'...")]
	public static partial void LogStoringInMemoryCache(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when an item has been stored in memory cache
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key that was stored</param>
	/// <param name="expiration">The expiration timespan of the cached item</param>
	[LoggerMessage(
	EventId = 1002,
	Level = LogLevel.Information,
	Message = "Value stored in memory cache with key '{cacheKey}' and expiration of {expiration}")]
	public static partial void LogStoredInMemoryCache(
		this ILogger logger,
		string cacheKey,
		TimeSpan expiration);

	/// <summary>
	///     Logs when attempting to retrieve an item from memory cache
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key being retrieved</param>
	[LoggerMessage(
	EventId = 1003,
	Level = LogLevel.Debug,
	Message = "Retrieving from memory cache with key '{cacheKey}'...")]
	public static partial void LogRetrievingFromMemoryCache(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when an item has been successfully retrieved from memory cache
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key that was retrieved</param>
	[LoggerMessage(
	EventId = 1004,
	Level = LogLevel.Information,
	Message = "Value successfully retrieved from memory cache for key '{cacheKey}'")]
	public static partial void LogRetrievedFromMemoryCache(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when an item is not found in memory cache
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key that was not found</param>
	[LoggerMessage(
	EventId = 1005,
	Level = LogLevel.Debug,
	Message = "No value found in memory cache for key '{cacheKey}'")]
	public static partial void LogNotFoundInMemoryCache(
		this ILogger logger,
		string cacheKey);

	#endregion

	#region Frozen Cache (2xxx)

	/// <summary>
	///     Logs when attempting to store an item in frozen cache
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key being stored</param>
	[LoggerMessage(
	EventId = 2001,
	Level = LogLevel.Debug,
	Message = "Attempting to store in frozen cache with key '{cacheKey}'...")]
	public static partial void LogStoringInFrozenCache(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when a key already exists in frozen cache
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The duplicate cache key</param>
	[LoggerMessage(
	EventId = 2002,
	Level = LogLevel.Debug,
	Message = "Key '{cacheKey}' already exists in frozen cache. Ignoring store operation")]
	public static partial void LogFrozenCacheKeyExists(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when an item has been stored in frozen cache
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key that was stored</param>
	[LoggerMessage(
	EventId = 2003,
	Level = LogLevel.Information,
	Message = "Value stored in frozen cache with key '{cacheKey}'")]
	public static partial void LogStoredInFrozenCache(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when attempting to retrieve an item from frozen cache
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key being retrieved</param>
	[LoggerMessage(
	EventId = 2004,
	Level = LogLevel.Debug,
	Message = "Attempting to retrieve from frozen cache with key '{cacheKey}'...")]
	public static partial void LogRetrievingFromFrozenCache(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when an item has been successfully retrieved from frozen cache
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key that was retrieved</param>
	[LoggerMessage(
	EventId = 2005,
	Level = LogLevel.Information,
	Message = "Value successfully retrieved from frozen cache for key '{cacheKey}'")]
	public static partial void LogRetrievedFromFrozenCache(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when an item is not found in frozen cache
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key that was not found</param>
	[LoggerMessage(
	EventId = 2006,
	Level = LogLevel.Debug,
	Message = "No value found in frozen cache for key '{cacheKey}'")]
	public static partial void LogNotFoundInFrozenCache(
		this ILogger logger,
		string cacheKey);

	#endregion

	#region Redis Cache (3xxx)

	/// <summary>
	///     Logs when Redis server connection is established
	/// </summary>
	/// <param name="logger">The logger instance</param>
	[LoggerMessage(
	EventId = 3001,
	Level = LogLevel.Information,
	Message = "Redis server connection established successfully")]
	public static partial void LogRedisConnected(
		this ILogger logger);

	/// <summary>
	///     Logs when Redis server connection fails
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="exception">The exception that occurred</param>
	[LoggerMessage(
	EventId = 3002,
	Level = LogLevel.Error,
	Message = "Failed to connect to Redis server")]
	public static partial void LogRedisConnectionFailed(
		this ILogger logger,
		Exception exception);

	/// <summary>
	///     Logs when attempting to connect to Redis server
	/// </summary>
	/// <param name="logger">The logger instance</param>
	[LoggerMessage(
	EventId = 3003,
	Level = LogLevel.Debug,
	Message = "Attempting to connect to Redis server...")]
	public static partial void LogRedisConnecting(
		this ILogger logger);

	/// <summary>
	///     Logs when Redis manager is already initialized
	/// </summary>
	/// <param name="logger">The logger instance</param>
	[LoggerMessage(
	EventId = 3004,
	Level = LogLevel.Debug,
	Message = "Redis manager is already initialized. Ignoring reinitialization")]
	public static partial void LogRedisAlreadyInitialized(
		this ILogger logger);

	/// <summary>
	///     Logs when Redis cache manager is configured with Aspire
	/// </summary>
	/// <param name="logger">The logger instance</param>
	[LoggerMessage(
	EventId = 3005,
	Level = LogLevel.Information,
	Message = "Redis cache manager configured to use Aspire integration")]
	public static partial void LogRedisAspireConfigured(
		this ILogger logger);

	/// <summary>
	///     Logs when failing to get Redis database from service provider
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="exception">The exception that occurred</param>
	[LoggerMessage(
	EventId = 3006,
	Level = LogLevel.Error,
	Message = "Failed to get Redis database from service provider")]
	public static partial void LogRedisGetDatabaseFailed(
		this ILogger logger,
		Exception exception);

	/// <summary>
	///     Logs when attempting to use Redis before initialization
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key being accessed</param>
	/// <param name="operation">The attempted operation</param>
	[LoggerMessage(
	EventId = 3007,
	Level = LogLevel.Warning,
	Message = "Attempt to {operation} in Redis with key '{cacheKey}' while Redis is not initialized")]
	public static partial void LogRedisNotInitialized(
		this ILogger logger,
		string cacheKey,
		string operation);

	/// <summary>
	///     Logs when storing an item in Redis
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key being stored</param>
	[LoggerMessage(
	EventId = 3008,
	Level = LogLevel.Debug,
	Message = "Storing in Redis with key '{cacheKey}'...")]
	public static partial void LogStoringInRedis(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when an item has been stored in Redis
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key that was stored</param>
	/// <param name="expiration">The expiration timespan of the cached item</param>
	[LoggerMessage(
	EventId = 3009,
	Level = LogLevel.Information,
	Message = "Value stored in Redis with key '{cacheKey}' and expiration {expiration}")]
	public static partial void LogStoredInRedis(
		this ILogger logger,
		string cacheKey,
		TimeSpan? expiration);

	/// <summary>
	///     Logs when storing an item in Redis fails
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key that failed to store</param>
	[LoggerMessage(
	EventId = 3010,
	Level = LogLevel.Warning,
	Message = "Failed to store value in Redis with key '{cacheKey}'")]
	public static partial void LogRedisStoreFailed(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when an error occurs storing in Redis
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key being stored</param>
	/// <param name="exception">The exception that occurred</param>
	[LoggerMessage(
	EventId = 3011,
	Level = LogLevel.Error,
	Message = "Error while storing in Redis with key '{cacheKey}'")]
	public static partial void LogRedisStoreError(
		this ILogger logger,
		string cacheKey,
		Exception exception);

	/// <summary>
	///     Logs when retrieving an item from Redis
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key being retrieved</param>
	[LoggerMessage(
	EventId = 3012,
	Level = LogLevel.Debug,
	Message = "Retrieving from Redis with key '{cacheKey}'...")]
	public static partial void LogRetrievingFromRedis(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when an item is not found in Redis
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key that was not found</param>
	[LoggerMessage(
	EventId = 3013,
	Level = LogLevel.Debug,
	Message = "No value found in Redis for key '{cacheKey}'")]
	public static partial void LogRedisNotFound(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when an item has been successfully retrieved from Redis
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key that was retrieved</param>
	[LoggerMessage(
	EventId = 3014,
	Level = LogLevel.Information,
	Message = "Value successfully retrieved from Redis for key '{cacheKey}'")]
	public static partial void LogRetrievedFromRedis(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when deserialization fails for a Redis value
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key that failed to deserialize</param>
	[LoggerMessage(
	EventId = 3015,
	Level = LogLevel.Warning,
	Message = "Deserialization failed for Redis key '{cacheKey}'")]
	public static partial void LogRedisDeserializationFailed(
		this ILogger logger,
		string cacheKey);

	/// <summary>
	///     Logs when an error occurs retrieving from Redis
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="cacheKey">The cache key being retrieved</param>
	/// <param name="exception">The exception that occurred</param>
	[LoggerMessage(
	EventId = 3016,
	Level = LogLevel.Error,
	Message = "Error while retrieving from Redis with key '{cacheKey}'")]
	public static partial void LogRedisRetrieveError(
		this ILogger logger,
		string cacheKey,
		Exception exception);

	#endregion

	#region Database Operations (4xxx)

	/// <summary>
	///     Logs when attempting to open a database connection
	/// </summary>
	/// <param name="logger">The logger instance</param>
	[LoggerMessage(
	EventId = 4001,
	Level = LogLevel.Debug,
	Message = "Attempting to open a database connection...")]
	public static partial void LogDatabaseConnecting(
		this ILogger logger);

	/// <summary>
	///     Logs when database connection is established
	/// </summary>
	/// <param name="logger">The logger instance</param>
	[LoggerMessage(
	EventId = 4002,
	Level = LogLevel.Information,
	Message = "Database connection established successfully")]
	public static partial void LogDatabaseConnected(
		this ILogger logger);

	/// <summary>
	///     Logs when database connection fails
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="exception">The exception that occurred</param>
	[LoggerMessage(
	EventId = 4003,
	Level = LogLevel.Error,
	Message = "Failed to connect to database")]
	public static partial void LogDatabaseConnectionFailed(
		this ILogger logger,
		Exception exception);

	/// <summary>
	///     Logs when executing a stored procedure
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="procedureName">The name of the stored procedure</param>
	/// <param name="parameterCount">The number of parameters</param>
	[LoggerMessage(
	EventId = 4004,
	Level = LogLevel.Debug,
	Message = "Executing stored procedure '{procedureName}' with {parameterCount} parameter(s)")]
	public static partial void LogExecutingStoredProcedure(
		this ILogger logger,
		string procedureName,
		int parameterCount);

	/// <summary>
	///     Logs when a stored procedure has been executed successfully
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="procedureName">The name of the stored procedure</param>
	/// <param name="elapsedMs">The elapsed time in milliseconds</param>
	[LoggerMessage(
	EventId = 4005,
	Level = LogLevel.Information,
	Message = "Stored procedure '{procedureName}' executed successfully in {elapsedMs}ms")]
	public static partial void LogStoredProcedureExecuted(
		this ILogger logger,
		string procedureName,
		long elapsedMs);

	/// <summary>
	///     Logs when stored procedure execution fails
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="procedureName">The name of the stored procedure</param>
	/// <param name="exception">The exception that occurred</param>
	[LoggerMessage(
	EventId = 4006,
	Level = LogLevel.Error,
	Message = "Failed to execute stored procedure '{procedureName}'")]
	public static partial void LogStoredProcedureExecutionFailed(
		this ILogger logger,
		string procedureName,
		Exception exception);

	/// <summary>
	///     Logs when reading result sets from a stored procedure
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="procedureName">The name of the stored procedure</param>
	/// <param name="resultSetCount">The number of result sets</param>
	[LoggerMessage(
	EventId = 4007,
	Level = LogLevel.Debug,
	Message = "Reading {resultSetCount} result set(s) from stored procedure '{procedureName}'")]
	public static partial void LogReadingResultSets(
		this ILogger logger,
		string procedureName,
		int resultSetCount);

	/// <summary>
	///     Logs when result sets have been read from a stored procedure
	/// </summary>
	/// <param name="logger">The logger instance</param>
	/// <param name="procedureName">The name of the stored procedure</param>
	/// <param name="rowCount">The number of rows read</param>
	[LoggerMessage(
	EventId = 4008,
	Level = LogLevel.Information,
	Message = "Read {rowCount} row(s) from stored procedure '{procedureName}'")]
	public static partial void LogResultSetRead(
		this ILogger logger,
		string procedureName,
		int rowCount);

	#endregion

}