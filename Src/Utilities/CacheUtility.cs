namespace CaeriusNet.Utilities;

/// <summary>
///     Provides utility methods for caching data using different caching mechanisms.
/// </summary>
static internal class CacheUtility
{
	private static IRedisCacheManager? _redisCacheManager;

	/// <summary>
	///     Sets the Redis cache manager from dependency injection.
	/// </summary>
	/// <param name="redisCacheManager">The Redis cache manager instance to set.</param>
	static internal void SetRedisCacheManager(IRedisCacheManager? redisCacheManager)
	{
		_redisCacheManager = redisCacheManager;
	}

	/// <summary>
	///     Attempts to retrieve a cached result for the given stored procedure parameters.
	/// </summary>
	/// <typeparam name="T">The type of the cached value.</typeparam>
	/// <param name="spParameters">The parameters of the stored procedure, including cache configuration.</param>
	/// <param name="result">The output parameter where the cached result will be stored if found.</param>
	/// <returns>
	///     <c>true</c> if a cached result is successfully retrieved; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	///     This method checks the cache type specified in <paramref name="spParameters" /> and attempts to retrieve
	///     the cached value from the appropriate cache store (Frozen, InMemory, or Redis).
	///     If the cache type is not specified or the cache key is empty, the method returns false.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	static internal bool TryRetrieveFromCache<T>(StoredProcedureParameters spParameters, out T? result)
	{
		result = default;

		if (!spParameters.CacheType.HasValue || string.IsNullOrEmpty(spParameters.CacheKey)) return false;

		return spParameters.CacheType.Value switch
		{
			Frozen => FrozenCacheManager.TryGet(spParameters.CacheKey, out result),
			InMemory => InMemoryCacheManager.TryGet(spParameters.CacheKey, out result),
			Redis => (_redisCacheManager?.IsInitialized ?? false) &&
			         _redisCacheManager.TryGet(spParameters.CacheKey, out result),
			_ => false
		};
	}

	/// <summary>
	///     Stores the specified result in a cache based on the provided stored procedure parameters.
	/// </summary>
	/// <typeparam name="T">The type of the result to be cached.</typeparam>
	/// <param name="spParameters">
	///     The stored procedure parameters containing cache key, cache type, and expiration information.
	/// </param>
	/// <param name="result">The result to be stored in the cache.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when an invalid cache type is specified in the parameters.
	/// </exception>
	/// <remarks>
	///     This method stores the result in the appropriate cache store based on the cache type specified
	///     in <paramref name="spParameters" />. The cache types supported are:
	///     <list type="bullet">
	///         <item>
	///             <description>Frozen - For read-only, precomputed cache</description>
	///         </item>
	///         <item>
	///             <description>InMemory - For in-process volatile cache</description>
	///         </item>
	///         <item>
	///             <description>Redis - For distributed cache backed by Redis</description>
	///         </item>
	///     </list>
	///     If the cache type is not specified or the cache key is empty, the method returns without storing.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static internal void StoreInCache<T>(StoredProcedureParameters spParameters, T result)
		where T : notnull
	{
		if (spParameters.CacheType is null || string.IsNullOrEmpty(spParameters.CacheKey)) return;

		switch (spParameters.CacheType.Value){
			case Frozen:
				FrozenCacheManager.Store(spParameters.CacheKey, result);
				break;

			case InMemory:
				InMemoryCacheManager.Store(spParameters.CacheKey, result, spParameters.CacheExpiration!.Value);
				break;

			case Redis:
				_redisCacheManager?.Store(spParameters.CacheKey, result, spParameters.CacheExpiration);
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}
}