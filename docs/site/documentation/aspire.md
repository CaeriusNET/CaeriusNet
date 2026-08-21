# Aspire integration

Install `CaeriusNet.Aspire` in the service that consumes Aspire SQL Server or Redis resources. The integration calls Aspire's SQL Server and Redis client registrations directly; it does not parse connection strings itself.

```bash
dotnet add package CaeriusNet.Aspire --version 11.2.0
```

## Service configuration

```csharp
using CaeriusNet.Aspire;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.AddCaeriusNetAspire(
    sqlConnectionName: "orders",
    configureCaerius: options => options.ApplicationNamespace = "shop",
    redisConnectionName: "cache");
```

`AddCaeriusNetAspire` registers the default `ICaeriusNetDbContext`. For a second database, register a keyed context:

```csharp
builder.AddKeyedCaeriusNetAspire(
    caeriusName: "reporting",
    sqlConnectionName: "reporting");

public sealed class ReportRepository(
    [FromKeyedServices("reporting")] ICaeriusNetDbContext database)
{
}
```

Each operation resolves the configured Aspire `SqlConnection` from a scope, creates an internal lease, and releases both when the operation ends. A transaction retains its lease until commit, rollback or disposal.

## Health and telemetry

The optional Caerius health check reads `SERVERPROPERTY('ProductMajorVersion')` once at startup and accepts only SQL Server 2022 (16) and 2025 (17). It does not run on each stored-procedure command.

Caerius exposes its `ActivitySource` and `Meter` names through `CaeriusTelemetry`. Aspire registers their listeners/exporters. The integration disables redundant low-level SqlClient tracing by default so an execution has one Caerius client span. Re-enable low-level spans in `CaeriusAspireOptions` only when diagnostic detail justifies duplicate spans.

Stable client attributes include `db.system.name=microsoft.sql_server`, `db.operation.name=EXECUTE`, `db.stored_procedure.name`, `db.namespace`, `server.address`, `db.response.status_code` and `error.type`. The duration histogram is `db.client.operation.duration` in seconds.

No connection string, cache key, parameter value or TVP value is emitted by default. Development parameter capture requires both an explicit allowlist and a redactor.
