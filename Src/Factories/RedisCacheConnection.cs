namespace CaeriusNet.Factories;

/// <summary>
///     Implementation of Redis cache connection using IConnectionMultiplexer
/// </summary>
public sealed class RedisCacheConnection : IRedisCacheConnection
{
	private readonly IConnectionMultiplexer _connectionMultiplexer;

	/// <summary>
	///     Initializes a new instance of the RedisCacheConnection class
	/// </summary>
	/// <param name="connectionMultiplexer">The Redis connection multiplexer from Aspire</param>
	public RedisCacheConnection(IConnectionMultiplexer connectionMultiplexer)
	{
		_connectionMultiplexer = connectionMultiplexer;
	}

	/// <summary>
	///     Gets the Redis database interface for cache operations
	/// </summary>
	public IDatabase GetDatabase()
	{
		return _connectionMultiplexer.GetDatabase();
	}

	/// <summary>
	///     Checks if the connection is initialized and available
	/// </summary>
	public bool IsConnected()
	{
		return _connectionMultiplexer.IsConnected;
	}
}