var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddSqlServer("sqlserver")
    .WithDataVolume("sqlserver-data");

// init.sql lives in Sql/init.sql (this project) so it is easy to find and edit.
// At runtime it is copied to the output directory alongside this binary.
var initSqlPath = Path.Combine(AppContext.BaseDirectory, "Sql", "init.sql");
var initSqlScript = File.ReadAllText(initSqlPath);

// Bootstrap the example schema/types/SPs as soon as the database is created.
// Aspire runs the script once when the container is provisioned (idempotent re-runs are
// handled by the script itself: every object is dropped before being re-created).
var caeriusNet = sqlServer
    .AddDatabase("CaeriusNet")
    .WithCreationScript(initSqlScript);

var redis = builder.AddRedis("redis");

builder.AddProject<CaeriusNet_Exemples_Aspire_Console>("ExempleProject")
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(caeriusNet)
    .WaitFor(caeriusNet);

builder.Build().Run();
