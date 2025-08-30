namespace CaeriusNet.Core.Caching.Types.Enums;

/// <summary>
///     Defines the type of caching mechanism available for data storage and retrieval.
///     Including memory-based, immutable frozen, and distributed Redis options.
/// </summary>
public enum CacheType : byte
{
	/// <summary>
	///     Uses volatile memory for cache storage with full read and write operations.
	///     This is the default caching mechanism that provides fast access but data is not persisted.
	/// </summary>
	InMemory,

	/// <summary>
	///     Uses an immutable frozen dictionary for read-only cache operations.
	///     Provides thread-safe access and optimal memory usage for static data.
	/// </summary>
	Frozen,

	/// <summary>
	///     Uses Redis as a distributed caching system for read-only operations.
	///     Enables cache sharing across multiple application instances and provides data persistence.
	/// </summary>
	Redis
}