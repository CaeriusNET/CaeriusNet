﻿namespace CaeriusNet.Utilities;

internal static class CacheUtility
{
	/// <summary>
	///     Sets the service provider for dependency injection in cache managers
	/// </summary>
	/// <param name="serviceProvider">The service provider instance</param>
	internal static void SetServiceProvider(IServiceProvider serviceProvider)
	{
		RedisCacheManager.SetServiceProvider(serviceProvider);
	}

	/// <summary>
	///     Attempts to retrieve a cached result for the given stored procedure parameters.
	/// </summary>
	/// <typeparam name="T">The type of the cached value.</typeparam>
	/// <param name="spParameters">The parameters of the stored procedure, including cache configuration.</param>
	/// <param name="result">The output parameter where the cached result will be stored if found.</param>
	/// <returns>
	///     true if a cached result is successfully retrieved; otherwise, false.
	/// </returns>
	internal static bool TryRetrieveFromCache<T>(StoredProcedureParameters spParameters, out T? result)
	{
		result = default;
		if (spParameters.CacheType is null || string.IsNullOrEmpty(spParameters.CacheKey)) return false;

		return spParameters.CacheType switch
		{
			InMemory => InMemoryCacheManager.TryGet(spParameters.CacheKey, out result),
			Frozen => FrozenCacheManager.TryGet(spParameters.CacheKey, out result),
			Redis => RedisCacheManager.IsInitialized() && RedisCacheManager.TryGet(spParameters.CacheKey, out result),
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
	/// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid cache type is specified in the parameters.</exception>
	internal static void StoreInCache<T>(StoredProcedureParameters spParameters, T result)
	{
		if (spParameters.CacheType is null || string.IsNullOrEmpty(spParameters.CacheKey)) return;

		switch (spParameters.CacheType)
		{
			case InMemory:
				InMemoryCacheManager.Store(spParameters.CacheKey, result, spParameters.CacheExpiration!.Value);
				break;
			case Frozen:
				FrozenCacheManager.Store(spParameters.CacheKey, result);
				break;
			case Redis:
				if (RedisCacheManager.IsInitialized())
					RedisCacheManager.Store(spParameters.CacheKey, result, spParameters.CacheExpiration);
				break;
			case null:
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}
}