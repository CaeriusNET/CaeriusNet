namespace CaeriusNet.Utilities;

/// <summary>
///     Defines the type of caching mechanism available for data storage and retrieval.
/// </summary>
public enum CacheType : byte
{
	/// <summary>
	///     Read-only, precomputed cache optimized for fast lookups.
	///     This cache type is immutable once populated and provides the fastest possible retrieval performance.
	/// </summary>
	Frozen,

	/// <summary>
	///     In-process volatile cache stored in memory.
	///     Data is stored in the application's memory space and is lost when the process terminates.
	///     Provides fast access but limited by available memory.
	/// </summary>
	InMemory,

	/// <summary>
	///     Distributed cache backed by Redis.
	///     Provides persistent storage and sharing across multiple application instances.
	///     Suitable for distributed systems requiring cache coherence.
	/// </summary>
	Redis
}