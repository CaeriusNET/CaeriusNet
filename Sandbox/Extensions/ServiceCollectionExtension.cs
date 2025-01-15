using CaeriusNet.Sandbox.Repositories;
using CaeriusNet.Sandbox.Repositories.Interfaces;
using CaeriusNet.Sandbox.Services;
using CaeriusNet.Sandbox.Services.Interfaces;

namespace CaeriusNet.Sandbox.Extensions;

public static class ServiceCollectionExtension
{
	public static IServiceCollection AddServices(this IServiceCollection services)
	{
		return services
			.AddScoped<ISandboxService, SandboxService>();
	}

	public static void AddRepositories(this IServiceCollection services)
	{
		services
			.AddScoped<ISandboxRepository, SandboxRepository>();
	}
}