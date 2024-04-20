using CaeriusNet.Factories;

namespace CaeriusNet.Extensions;

public static class CaeriusServiceCollectionExtensions
{
    public static IServiceCollection RegisterCaeriusOrm(this IServiceCollection services, string connectionString)
    {
        return services
            .AddSingleton<ICaeriusDbConnectionFactory, CaeriusDbConnectionFactory>(_ =>
                new CaeriusDbConnectionFactory(connectionString));
    }
}