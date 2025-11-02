var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddSqlServer("sqlserver")
	.WithDataVolume("sqlserver-data")
	.AddDatabase("CaeriusNet");

var redis = builder.AddRedis("redis");

builder.AddProject<CaeriusNet_Exemples_Aspire_Console>("ExempleProject")
	.WithReference(redis)
	.WaitFor(redis)
	.WithReference(sqlServer)
	.WaitFor(sqlServer);

builder.Build().Run();