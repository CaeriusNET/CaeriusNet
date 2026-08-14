namespace CaeriusNet.Examples.Common.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddDependenciesInjections(this IServiceCollection services)
    {
        return services
            .AddUserDtoCaeriusCodec()
            .AddOrderDtoCaeriusCodec()
            .AddUserStatsDtoCaeriusCodec()
            .AddScoped<IUsersRepository, UsersRepository>()
            .AddScoped<IUsersService, UsersService>();
    }
}
