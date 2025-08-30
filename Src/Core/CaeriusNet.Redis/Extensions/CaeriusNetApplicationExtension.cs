namespace CaeriusNet.Redis.Extensions;

/// <summary>
///     Dependency Injection helpers for registering the Redis single-connection provider and client facade.
/// </summary>
public static class CaeriusNetApplicationExtension
{
	/// <summary>
	///     Registers Redis services with a mandatory connection string and an optional default database index.
	///     This configures a single long-lived facade.
	/// </summary>
	/// <param name="services">The application services builder.</param>
	/// <param name="redisConnectionString">Mandatory Redis connection string (non-empty).</param>
	/// <param name="defaultDatabase">Default database index for the connection; use -1 to keep provider default.</param>
	/// <returns>The original application builder.</returns>
	public static ICaeriusNetApplication AddRedisCache(this ICaeriusNetApplication services,
        string redisConnectionString, int defaultDatabase = -1)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(redisConnectionString))
            throw new ArgumentException("Redis connection string must be a non-empty value.",
                nameof(redisConnectionString));

        // Delegate to the underlying container registration API.
        // The ICaeriusNetApplication is expected to expose an IServiceCollection through extensions.
        services
            .AddSingleton<IRedisConnectionProvider>(_ =>
                new RedisConnectionProvider(redisConnectionString, defaultDatabase));

        // Optionally: eager warm-up can be triggered by the host if desired by resolving IRedisCacheClient and calling EnsureConnectedAsync.

        return services;
    }
}