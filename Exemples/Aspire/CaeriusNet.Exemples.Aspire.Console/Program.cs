var builder = new HostApplicationBuilder();

builder.AddServiceDefaults();

CaeriusNetBuilder.Create(builder)
    .WithAspireSqlServer("CaeriusNet")
    .WithAspireRedis()
    .Build();

builder.Services
    .AddDependenciesInjections()
    .AddLogging(loggingBuilder => loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Debug));

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
LoggerProvider.SetLogger(logger);

var usersRepository = app.Services.GetRequiredService<IUsersRepository>();

var getAllUsers = await usersRepository.GetAllUsers();

var getAllUsersWithRedisCache1 = await usersRepository.GetAllUsersWithRedisCache();
var getAllUsersWithRedisCache2 = await usersRepository.GetAllUsersWithRedisCache();

await app.RunAsync();