namespace CaeriusNet.Extensions;

/// <summary>
///     Extension methods for IServiceCollection to support CaeriusNET ORM registration.
/// </summary>
public static class CaeriusServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the Caerius ORM database connection factory in the service collection (service provider).
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <returns>The IServiceCollection for chaining.</returns>
    public static IServiceCollection AddCaeriusNet(this IServiceCollection services, string connectionString)
    {
        return services.AddSingleton<ICaeriusDbContext, CaeriusDbContext>(_ => new CaeriusDbContext(connectionString));
    }
}