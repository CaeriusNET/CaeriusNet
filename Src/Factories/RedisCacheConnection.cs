namespace CaeriusNet.Factories;

/// <summary>
///     Implementation of Redis cache connection using IConnectionMultiplexer
/// </summary>
public sealed class RedisCacheConnection : IRedisCacheConnection
{
	/// <summary>
	///     The Redis connection multiplexer instance
	/// </summary>
	private readonly IConnectionMultiplexer _connectionMultiplexer;

	/// <summary>
	///     The Redis database instance
	/// </summary>
	private readonly IDatabase _database;

	/// <summary>
	///     Initializes a new instance of the RedisCacheConnection class
	/// </summary>
	/// <param name="connectionMultiplexer">The Redis connection multiplexer from Aspire</param>
	/// <exception cref="ArgumentNullException">Thrown when connectionMultiplexer is null</exception>
	public RedisCacheConnection(IConnectionMultiplexer connectionMultiplexer)
	{
		_connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
		_database = _connectionMultiplexer.GetDatabase();
	}

	/// <summary>
	///     Gets the Redis database interface for cache operations
	/// </summary>
	/// <returns>An IDatabase instance for Redis operations</returns>
	public IDatabase GetDatabase()
	{
		return _database;
	}

	/// <summary>
	///     Checks if the connection is initialized and available
	/// </summary>
	/// <returns>True if the connection is established, false otherwise</returns>
	public bool IsConnected()
	{
		return _connectionMultiplexer.IsConnected;
	}
}