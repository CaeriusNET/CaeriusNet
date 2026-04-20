namespace CaeriusNet.Abstractions;

/// <summary>
///     Public DI-friendly façade over the three internal cache tiers (Frozen, InMemory, Redis).
///     Use it from application services to invalidate or flush cache entries when the underlying
///     SQL Server data changes (e.g. after a write outside of the read-cached path).
/// </summary>
/// <remarks>
///     <para>
///         The library still owns the actual cache stores; this façade is intentionally thin and
///         exposes only invalidation primitives. Population happens transparently inside the read
///         extensions whenever <c>AddInMemoryCache</c> / <c>AddFrozenCache</c> / <c>AddRedisCache</c>
///         is configured on the <see cref="Builders.StoredProcedureParametersBuilder" />.
///     </para>
///     <para>
///         For Redis, <see cref="ClearAsync" /> is intentionally NOT supported — clearing a shared
///         distributed cache is dangerous and outside the responsibility of this library. Use a
///         targeted <see cref="RemoveAsync(string,CacheType)" /> or operate directly on Redis.
///     </para>
/// </remarks>
public interface ICaeriusNetCache
{
    /// <summary>
    ///     Removes the specified key from every local cache tier (Frozen and InMemory) and from
    ///     Redis if it is configured. Use this after a write that invalidates a cached read.
    /// </summary>
    ValueTask RemoveAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes the specified key from a single cache tier.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="cacheType" /> is not a known value.</exception>
    /// <exception cref="NotSupportedException">When attempting to clear Redis (use <see cref="RemoveAsync(string,CancellationToken)" /> instead).</exception>
    ValueTask RemoveAsync(string cacheKey, CacheType cacheType, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Empties the specified local cache tier. Not supported for <see cref="CacheType.Redis" />.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="cacheType" /> is not a known value.</exception>
    /// <exception cref="NotSupportedException">When attempting to clear Redis.</exception>
    ValueTask ClearAsync(CacheType cacheType, CancellationToken cancellationToken = default);
}
