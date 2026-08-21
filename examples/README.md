# CaeriusNet examples

The example applications are real consumers of the package-oriented source tree. They demonstrate a conventional
stored-procedure console application and an Aspire-hosted application without relying on project paths outside this
directory.

## Projects

| Project                                                                 | Scenario       | Description                                                                           |
|-------------------------------------------------------------------------|----------------|---------------------------------------------------------------------------------------|
| `Common/CaeriusNet.Examples.Common`                                     | Shared         | DTOs, TVPs, repositories, bootstrapper and DI registration used by both applications. |
| `StoredProcedures.Console/CaeriusNet.Examples.StoredProcedures.Console` | Traditional    | Console application with manually configured SQL Server and optional Redis.           |
| `Aspire/CaeriusNet.Examples.Aspire.AppHost`                             | Cloud-native   | Aspire orchestration for SQL Server and Redis.                                        |
| `Aspire/CaeriusNet.Examples.Aspire.Console`                             | Cloud-native   | Hosted worker that consumes Aspire-managed resources.                                 |
| `Aspire/CaeriusNet.Examples.Aspire.ServiceDefaults`                     | Shared hosting | Reusable Aspire health, telemetry and service-default configuration.                  |

## Prerequisites

- .NET SDK `10.0.400` or a later patch accepted by [global.json](../global.json)
- SQL Server 2022 (major version 16) or SQL Server 2025 (major version 17)
- Docker Desktop or Podman for the Aspire application

The runtime itself executes stored procedures only. The sample bootstrapper is the one intentional setup component that
applies `Sql/init.sql` before the application makes runtime calls.

## Run the stored-procedure console example

```powershell
dotnet run --project examples/StoredProcedures.Console/CaeriusNet.Examples.StoredProcedures.Console/CaeriusNet.Examples.StoredProcedures.Console.csproj
```

Update the console project's `appsettings.json` first:

- Set `ConnectionStrings:DefaultConnection` to a supported SQL Server instance. Production strings must use
  `Encrypt=Strict;TrustServerCertificate=False` and a certificate trusted by the host.
- Optionally set `ConnectionStrings:Redis`; leaving it empty demonstrates the SQL fallback path.

## Run the Aspire example

```powershell
dotnet run --project examples/Aspire/CaeriusNet.Examples.Aspire.AppHost/CaeriusNet.Examples.Aspire.AppHost.csproj
```

The AppHost provisions SQL Server and Redis, starts the console worker, and enables the optional start-up check for SQL
Server major versions 16 and 17.

## Shared application code

`CaeriusNet.Examples.Common` contains the code shared by the two applications.

| Folder          | Purpose                                                             |
|-----------------|---------------------------------------------------------------------|
| `Abstractions/` | Repository contracts and dashboard snapshots.                       |
| `Bootstrap/`    | `ExampleDatabaseBootstrapper`, which applies the sample SQL script. |
| `Models/`       | `[GenerateDto]` DTOs and `[GenerateTvp]` row contracts.             |
| `Repositories/` | Implementations built on the DI-registered CaeriusNet runtime.      |
| `Services/`     | Application services used by the workers.                           |
| `Extensions/`   | Dependency-injection registration helpers.                          |

The embedded `Sql/init.sql` script lives with the AppHost and defines the schemas, tables, TVP types, and stored
procedures needed by the examples. It is idempotent so local development can start from a clean database repeatedly.

## Covered scenarios

Both applications exercise the same repository behavior:

- Materialized reads with memory, Frozen snapshot, and optional Redis caching.
- TVP reads, including empty and typed input.
- Multi-result procedures with explicit result-set contracts.
- Transaction commit, client-side rollback, and SQL-side rollback handling.
- CaeriusNet telemetry with parameter, connection-string, TVP-value, and cache-key capture disabled.

For package APIs, see the repository [README](../README.md) and
the [v11 to v12 migration guide](../docs/MIGRATION-v11-v12.md).

## Layout

```text
examples/
├── Aspire/
│   ├── CaeriusNet.Examples.Aspire.AppHost/
│   ├── CaeriusNet.Examples.Aspire.Console/
│   └── CaeriusNet.Examples.Aspire.ServiceDefaults/
├── Common/
│   └── CaeriusNet.Examples.Common/
└── StoredProcedures.Console/
    └── CaeriusNet.Examples.StoredProcedures.Console/
```
