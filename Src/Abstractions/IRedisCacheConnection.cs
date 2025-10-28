namespace CaeriusNet.Abstractions;

/// <summary>
///     Interface for Redis cache connection management.
/// </summary>
public interface IRedisCacheConnection
{
	/// <summary>
	///     Gets the Redis database interface for cache operations.
	/// </summary>
	/// <returns>
	///     An <see cref="IDatabase" /> instance that provides access to Redis database operations.
	/// </returns>
	IDatabase GetDatabase();

	/// <summary>
	///     Checks if the connection is initialized and available.
	/// </summary>
	/// <returns>
	///     <see langword="true" /> if the connection is established and ready;
	///     otherwise, <see langword="false" />.
	/// </returns>
	bool IsConnected();
}