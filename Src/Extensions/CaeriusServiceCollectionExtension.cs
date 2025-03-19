namespace CaeriusNet.Extensions;

/// <summary>
///     Provides extension methods for IServiceCollection to register and configure services
///     for the CaeriusNET ORM.
/// </summary>
public static class CaeriusServiceCollectionExtension
{
	/// <summary>
	///     Registers the Caerius ORM database connection factory in the service collection (service provider).
	/// </summary>
	/// <param name="services">The IServiceCollection to which the Caerius ORM dependencies will be registered.</param>
	/// <param name="connectionString">The database connection string used to establish the connection.</param>
	/// <returns>The IServiceCollection instance for method chaining.</returns>
	public static IServiceCollection AddCaeriusNet(this IServiceCollection services, string connectionString)
	{
		return services.AddSingleton<ICaeriusDbContext, CaeriusDbContext>(_ => new CaeriusDbContext(connectionString));
	}

	/// <summary>
	///     Registers the Caerius Redis cache in the service collection (service provider).
	/// </summary>
	/// <param name="services">The IServiceCollection to which the Caerius Redis cache dependencies will be registered.</param>
	/// <param name="redisConnectionString">The Redis connection string used to establish the connection to the Redis server.</param>
	/// <returns>The IServiceCollection instance for method chaining.</returns>
	public static IServiceCollection AddCaeriusRedisCache(this IServiceCollection services,
		string redisConnectionString)
	{
		if (!string.IsNullOrWhiteSpace(redisConnectionString)) RedisCacheManager.Initialize(redisConnectionString);

		return services;
	}
}