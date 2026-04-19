var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddSqlServer("sqlserver")
    .WithDataVolume("sqlserver-data");

var caeriusNet = sqlServer
    .AddDatabase("CaeriusNet");

var redis = builder.AddRedis("redis");

builder.AddProject<CaeriusNet_Exemples_Aspire_Console>("ExempleProject")
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(caeriusNet)
    .WaitFor(caeriusNet);

builder.Build().Run();