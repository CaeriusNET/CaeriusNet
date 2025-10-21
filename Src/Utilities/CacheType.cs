namespace CaeriusNet.Utilities;

/// <summary>
///     Defines the type of caching mechanism available for data storage and retrieval.
/// </summary>
public enum CacheType : byte
{
    /// <summary>
    ///     Read-only, precomputed cache optimized for fast lookups.
    /// </summary>
    Frozen,

    /// <summary>
    ///     In-process volatile cache stored in memory.
    /// </summary>
    InMemory,

    /// <summary>
    ///     Distributed cache backed by Redis.
    /// </summary>
    Redis
}