namespace CaeriusNet.Extensions.Caching.Types.Enums;

/// <summary>
///     Defines the type of caching mechanism available for data storage and retrieval.
/// </summary>
public enum CacheType : byte
{
	Frozen,
	InMemory,
	Redis
}