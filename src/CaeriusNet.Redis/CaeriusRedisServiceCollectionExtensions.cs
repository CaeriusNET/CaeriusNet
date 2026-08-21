using CaeriusNet.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaeriusNet.Redis;

public static class CaeriusRedisServiceCollectionExtensions
{
    public static IServiceCollection AddCaeriusNetRedis(
        this IServiceCollection services,
        Action<CaeriusRedisOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        // CaeriusNet.Redis can be registered in a minimal IServiceCollection, outside a Host.
        // Keep external logging registrations intact while allowing that supported scenario to resolve its logger.
        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        if (configure is not null)
            services.Configure(configure);
        else
            services.AddOptions<CaeriusRedisOptions>();
        services.TryAddSingleton<IRedisCacheManager, RedisCacheManager>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ICaeriusRemoteCacheManager, RedisRemoteCacheManagerRegistration>());
        return services;
    }
}
