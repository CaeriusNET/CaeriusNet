using CaeriusNet.Exemples.Interfaces;
using CaeriusNet.Exemples.Repositories;
using CaeriusNet.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var configuration = new ConfigurationBuilder()
	.AddJsonFile("appsettings.json", false, false)
	.Build();

var mssqlCnx = configuration.GetConnectionString("Database")!;

var redisCnx = configuration.GetConnectionString("Redis")!;

var serviceCollection = new ServiceCollection()
	.AddCaeriusNet(mssqlCnx)
	.AddCaeriusRedisCache(redisCnx)
	.AddCaeriusLoggingConsole()
	.AddScoped<IUsersRepositories, UsersRepositories>();

var serviceProvider = serviceCollection.BuildServiceProvider();