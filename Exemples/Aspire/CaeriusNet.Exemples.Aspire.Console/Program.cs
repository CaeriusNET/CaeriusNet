using CaeriusNet.Exemples.Aspire.Console;

var builder = new HostApplicationBuilder();

builder.AddServiceDefaults();

CaeriusNetBuilder.Create(builder)
    .WithAspireSqlServer("CaeriusNet")
    .WithAspireRedis()
    .WithTelemetryOptions(new CaeriusTelemetryOptions { CaptureParameterValues = true })
    .Build();

builder.Services
    .AddDependenciesInjections()
    .AddLogging(loggingBuilder => loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Information))
    // DemoWorker runs after the host starts, ensuring the OTel TracerProvider is active
    // before any SQL call is made (HasListeners() returns true → spans are exported).
    .AddHostedService<DemoWorker>();

var app = builder.Build();

LoggerProvider.SetLogger(app.Services.GetRequiredService<ILogger<Program>>());

await app.RunAsync();
