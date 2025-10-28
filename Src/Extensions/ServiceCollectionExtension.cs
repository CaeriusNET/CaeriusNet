namespace CaeriusNet.Extensions;

/// <summary>
///     Provides extension methods for IServiceCollection to register and configure services
///     for the CaeriusNET ORM.
/// </summary>
public static class ServiceCollectionExtension
{
	/// <summary>
	///     Registers the Caerius ORM database connection factory in the service collection.
	/// </summary>
	/// <param name="services">The IServiceCollection to add services to.</param>
	/// <param name="connectionString">A connection string used to establish the database connection.</param>
	/// <returns>The IServiceCollection so that additional calls can be chained.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services" /> is null.</exception>
	/// <exception cref="ArgumentException"><paramref name="connectionString" /> is null or whitespace.</exception>
	/// <remarks>
	///     This method registers <see cref="ICaeriusNetDbContext" /> as a singleton service that creates a new database
	///     context
	///     using the provided connection string. The context provides access to database operations and queries.
	/// </remarks>
	public static IServiceCollection AddCaeriusNet(this IServiceCollection services, string connectionString)
	{
		return services.AddSingleton<ICaeriusNetDbContext, CaeriusNetDbContext>(_ => new CaeriusNetDbContext(connectionString));
	}

	/// <summary>
	///     Registers the Caerius Redis cache services in the service collection.
	/// </summary>
	/// <param name="services">The IServiceCollection to add services to.</param>
	/// <param name="redisConnectionString">A Redis connection string used to connect to the Redis server.</param>
	/// <returns>The IServiceCollection so that additional calls can be chained.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services" /> is null.</exception>
	/// <remarks>
	///     This method registers <see cref="IRedisCacheManager" /> as a singleton service that manages Redis caching
	///     operations.
	///     If <paramref name="redisConnectionString" /> is null, empty or whitespace, no services will be registered.
	/// </remarks>
	public static IServiceCollection AddCaeriusNetRedisCache(this IServiceCollection services,
		string redisConnectionString)
	{
		if (!string.IsNullOrWhiteSpace(redisConnectionString))
			services.AddSingleton<IRedisCacheManager>(sp =>
				new RedisCacheManager(
				redisConnectionString,
				sp.GetService<ILogger<RedisCacheManager>>()));

		return services;
	}

	/// <summary>
	///     Registers Caerius ORM services with Aspire SQL Server integration.
	/// </summary>
	/// <param name="builder">The application builder to configure services with.</param>
	/// <param name="connectionString">A connection string used to establish the database connection.</param>
	/// <exception cref="ArgumentNullException"><paramref name="builder" /> is null.</exception>
	/// <exception cref="ArgumentException"><paramref name="connectionString" /> is null or whitespace.</exception>
	/// <remarks>
	///     This method:
	///     <list type="bullet">
	///         <item>
	///             <description>Adds SQL Server client using Aspire's built-in configuration</description>
	///         </item>
	///         <item>
	///             <description>Registers <see cref="ICaeriusNetDbContext" /> as a singleton service</description>
	///         </item>
	///     </list>
	/// </remarks>
	public static void AddCaeriusNetWithAspire(this IHostApplicationBuilder builder, string connectionString)
	{
		// Add SQL Server client using Aspire
		builder.AddSqlServerClient(connectionString);

		// Register Caerius DB context using the connection string from configuration
		builder.Services.AddSingleton<ICaeriusNetDbContext, CaeriusNetDbContext>(_ => new CaeriusNetDbContext(connectionString));
	}

	/// <summary>
	///     Registers Caerius Redis cache services with Aspire Redis integration.
	/// </summary>
	/// <param name="builder">The application builder to configure services with.</param>
	/// <param name="connectionName">The name of the Redis connection in Aspire configuration.</param>
	/// <exception cref="ArgumentNullException"><paramref name="builder" /> is null.</exception>
	/// <remarks>
	///     This method:
	///     <list type="bullet">
	///         <item>
	///             <description>Adds Redis client using Aspire's built-in configuration</description>
	///         </item>
	///         <item>
	///             <description>Registers <see cref="IRedisCacheConnection" /> as a singleton service</description>
	///         </item>
	///         <item>
	///             <description>Registers <see cref="IRedisCacheManager" /> as a singleton service</description>
	///         </item>
	///     </list>
	/// </remarks>
	public static void AddCaeriusNetWithAspireRedis(this IHostApplicationBuilder builder,
		string connectionName = "cache")
	{
		builder.AddRedisClient(connectionName);

		builder.Services.AddSingleton<IRedisCacheConnection>(provider =>
			new RedisCacheConnection(provider.GetRequiredService<IConnectionMultiplexer>()));

		builder.Services.AddSingleton<IRedisCacheManager>(provider =>
			new RedisCacheManager(
			provider.GetRequiredService<IRedisCacheConnection>(),
			provider.GetService<ILogger<RedisCacheManager>>()));
	}
}