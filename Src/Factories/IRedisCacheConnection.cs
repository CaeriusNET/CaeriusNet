namespace CaeriusNet.Factories;

/// <summary>
///     Interface for Redis cache connection management
/// </summary>
public interface IRedisCacheConnection
{
	/// <summary>
	///     Gets the Redis database interface for cache operations
	/// </summary>
	IDatabase GetDatabase();
    
	/// <summary>
	///     Checks if the connection is initialized and available
	/// </summary>
	bool IsConnected();
}