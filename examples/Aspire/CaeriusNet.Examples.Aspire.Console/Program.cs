using CaeriusNet.Examples.Aspire.Console;

var builder = new HostApplicationBuilder();

builder.AddServiceDefaults();

builder.AddCaeriusNetAspire(
    "CaeriusNet",
    configureAspire: options => options.EnableSqlServerVersionHealthCheck = true,
    redisConnectionName: "redis");

builder.Services
    .AddDependenciesInjections()
    .AddLogging(loggingBuilder => loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Information))
    // DemoWorker runs after the host starts, ensuring the OTel TracerProvider is active
    // before any SQL call is made (HasListeners() returns true → spans are exported).
    .AddHostedService<DemoWorker>();

var app = builder.Build();

await app.RunAsync();
