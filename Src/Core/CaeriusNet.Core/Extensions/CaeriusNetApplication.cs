namespace CaeriusNet.Core.Extensions;

/// <summary>
///     Provides extension methods for IServiceCollection to register and configure services
///     for the CaeriusNET ORM.
/// </summary>
public static class CaeriusNetApplication
{
	/// <summary>
	///     Registers the Caerius ORM database connection factory in the service collection (service provider).
	/// </summary>
	/// <param name="services">The IServiceCollection to which the Caerius ORM dependencies will be registered.</param>
	/// <param name="connectionString">The database connection string used to establish the connection.</param>
	/// <returns>The IServiceCollection instance for method chaining.</returns>
	public static ICaeriusNetApplication AddCaeriusNet(this IServiceCollection services, string connectionString)
    {
        return (ICaeriusNetApplication)services.AddSingleton<ICaeriusNetDbContext, CaeriusNetDbContext>(_ =>
            new CaeriusNetDbContext(connectionString));
    }
}