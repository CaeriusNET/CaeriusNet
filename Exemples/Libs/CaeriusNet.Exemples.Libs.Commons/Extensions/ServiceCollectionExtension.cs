namespace CaeriusNet.Exemples.Libs.Commons.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddDependenciesInjections(this IServiceCollection services)
    {
        return services.AddScoped<IUsersRepository, UsersRepository>();
    }
}