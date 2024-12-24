using CaeriusNet.Extensions;
using CaeriusNet.Sandbox.Extensions;
using CaeriusNet.Sandbox.Services.Interfaces;
using Microsoft.Extensions.Configuration;

#region > Dependency Injection Container

var services = new ServiceCollection();

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", true, true)
    .Build();

services.AddSingleton<IConfiguration>(configuration);

var connectionString = configuration.GetConnectionString("SandboxConnection");

if (connectionString != null)
{
    services
        .AddCaeriusNet(connectionString)
        .AddServices()
        .AddRepositories();
}
else
{
    Console.WriteLine("Connection string not found in appsettings.json");

    services
        .AddServices()
        .AddRepositories();
}

var serviceProvider = services.BuildServiceProvider();

#endregion

var sandboxService = serviceProvider.GetRequiredService<ISandboxService>();

await sandboxService.GetUsersTesting();

// await sandboxService.CreateListOfUsers();

// var users = await sandboxService.GetUsers();

// await sandboxService.UpdateRandomUserAge(users);

Console.ReadLine();