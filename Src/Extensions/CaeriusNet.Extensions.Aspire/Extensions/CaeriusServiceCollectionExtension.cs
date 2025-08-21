using CaeriusNet.Core.Factories;
using CaeriusNet.Extensions.Caching.Caches;
using CaeriusNet.Extensions.Caching.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace CaeriusNet.Extensions.Aspire.Extensions;

/// <summary>
///     Provides extension methods for IServiceCollection to register and configure services
///     for the CaeriusNET ORM.
/// </summary>
public static class CaeriusServiceCollectionExtension
{
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
	public static void AddCaeriusNetWithAspireAndRedis(this IHostApplicationBuilder builder,
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