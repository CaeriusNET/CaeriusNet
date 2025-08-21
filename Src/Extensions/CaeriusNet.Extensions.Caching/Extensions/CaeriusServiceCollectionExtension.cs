using CaeriusNet.Extensions.Caching.Caches;
using Microsoft.Extensions.DependencyInjection;

namespace CaeriusNet.Extensions.Caching.Extensions;

/// <summary>
///     Provides extension methods for IServiceCollection to register and configure services
///     for the CaeriusNET ORM.
/// </summary>
public static class CaeriusServiceCollectionExtension
{
	/// <summary>
	///     Registers the Caerius Redis cache in the service collection (service provider).
	/// </summary>
	/// <param name="services">The IServiceCollection to which the Caerius Redis cache dependencies will be registered.</param>
	/// <param name="redisConnectionString">The Redis connection string used to establish the connection to the Redis server.</param>
	/// <returns>The IServiceCollection instance for method chaining.</returns>
	public static IServiceCollection AddCaeriusRedisCache(this IServiceCollection services,
		string redisConnectionString)
	{
		if (!string.IsNullOrWhiteSpace(redisConnectionString))
			RedisCacheManager.Initialize(redisConnectionString);

		return services;
	}
}