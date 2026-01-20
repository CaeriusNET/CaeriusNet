var configuration = new ConfigurationBuilder()
	.AddJsonFile("appsettings.json")
	.Build();

var sqlConnectionString = configuration.GetConnectionString("DefaultConnection")!;

var serviceCollection = new ServiceCollection()
	.AddDependenciesInjections()
	.AddLogging(loggingBuilder => loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Debug));

CaeriusNetBuilder
	.Create(serviceCollection)
	.WithSqlServer(sqlConnectionString)
	.WithRedis("localhost:4567")
	.Build();

var serviceProvider = serviceCollection.BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
LoggerProvider.SetLogger(logger);

var usersRepository = serviceProvider.GetRequiredService<IUsersRepository>();

var getAllUsers = await usersRepository.GetAllUsers();

foreach (var user in getAllUsers)
	Console.WriteLine(user);

Console.ReadLine();