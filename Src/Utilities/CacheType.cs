namespace CaeriusNet.Utilities;

/// <summary>
///     Defines the type of caching mechanism available for data storage and retrieval.
/// </summary>
public enum CacheType : byte
{
	Frozen,
	InMemory,
	Redis,
	AspireRedis
}