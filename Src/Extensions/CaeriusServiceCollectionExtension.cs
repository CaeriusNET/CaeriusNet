using CaeriusNet.Logging;
using Microsoft.Extensions.Hosting;

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
		if (!string.IsNullOrWhiteSpace(redisConnectionString))
			RedisCacheManager.Initialize(redisConnectionString);

		return services;
	}

	/// <summary>
	///     Registers and configures the Caerius console logging service in the service collection.
	/// </summary>
	/// <param name="services">The IServiceCollection to which the console logger will be registered.</param>
	/// <returns>The IServiceCollection instance for method chaining.</returns>
	public static IServiceCollection AddCaeriusLoggingConsole(this IServiceCollection services)
	{
		var logger = new ConsoleLogger();
		LoggerProvider.SetLogger(logger);
		return services.AddSingleton<ICaeriusLogger>(logger);
	}

	/// <summary>
	///     Registers the Caerius ORM with Aspire SQL Server integration.
	/// </summary>
	/// <param name="builder">The host application builder.</param>
	/// <param name="connectionString">The connection string in configuration.</param>
	/// <returns>The host application builder instance for method chaining.</returns>
	public static void AddCaeriusNetWithAspire(this IHostApplicationBuilder builder, string connectionString)
	{
		// Add SQL Server client using Aspire
		builder.AddSqlServerClient(connectionString);

		// Register Caerius DB context using the connection string from configuration
		builder.Services.AddSingleton<ICaeriusDbContext, CaeriusDbContext>(_ => new CaeriusDbContext(connectionString));
	}

	/// <summary>
	///     Registers the Caerius ORM with Aspire Redis integration.
	/// </summary>
	/// <param name="builder">The host application builder.</param>
	/// <param name="connectionName">The connection name used in Aspire Redis configuration.</param>
	/// <returns>The host application builder instance for method chaining.</returns>
	public static void AddCaeriusNetWithAspireRedis(this IHostApplicationBuilder builder,
		string connectionName = "cache")
	{
		// Add Redis client using Aspire
		builder.AddRedisClient(connectionName);

		// Register the Redis connection multiplexer with RedisCacheManager
		builder.Services.AddSingleton<IRedisCacheConnection>(provider =>
			new RedisCacheConnection(provider.GetRequiredService<IConnectionMultiplexer>()));

		RedisCacheManager.UseAspireIntegration();
	}
}