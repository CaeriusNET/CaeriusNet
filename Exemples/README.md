# CaeriusNet Examples

## Overview

Working example projects show how to use CaeriusNet in traditional and .NET Aspire hosting scenarios.

## Projects

| Project         | Scenario     | Description                                                       |
|-----------------|--------------|-------------------------------------------------------------------|
| Aspire AppHost  | Cloud-native | .NET Aspire orchestration for SQL Server and Redis resources      |
| Aspire Console  | Cloud-native | Console app that consumes Aspire-managed resources                |
| Default Console | Traditional  | Console app with a manually configured SQL connection string      |
| Libs Commons    | Shared       | DTOs, repositories, abstractions, and models used by the examples |

> [!NOTE]
> The Aspire sample also includes `CaeriusNet.Exemples.Aspire.ServiceDefaults`, which provides shared Aspire service
> defaults.

## Prerequisites

- .NET 10 SDK
- SQL Server 2019 or later, local or containerized
- For Aspire examples: install the Aspire workload

```bash
dotnet workload install aspire
```

## Getting Started

### Default console example

```bash
cd Exemples/Default/CaeriusNet.Exemples.Default.Console
dotnet run
```

Update `appsettings.json` before running:

- Replace the SQL Server placeholders in `ConnectionStrings:DefaultConnection`
- Configure Redis if you want to use the Redis cache sample

### Aspire example

```bash
cd Exemples/Aspire/CaeriusNet.Exemples.Aspire.AppHost
dotnet run
```

The AppHost starts SQL Server and Redis resources automatically, then launches the console app with the generated
resource bindings.

## Shared Library (`Libs`)

`CaeriusNet.Exemples.Libs.Commons` contains the shared application code used by both console examples.

| Folder          | Purpose                                                                                         |
|-----------------|-------------------------------------------------------------------------------------------------|
| `Abstractions/` | Repository contracts (`IUsersRepository`, `DashboardSnapshot`)                                  |
| `Bootstrap/`    | `ExampleDatabaseBootstrapper` — applies `Sql/init.sql` to any SQL Server connection             |
| `Models/`       | DTOs with `[GenerateDto]` (`UserDto`, `OrderDto`, `UserStatsDto`) and TVPs with `[GenerateTvp]` |
| `Repositories/` | Repository implementations built on CaeriusNet                                                  |
| `Sql/`          | `init.sql` — schema + tables + TVP types + 9 stored procedures (embedded resource)              |
| `Extensions/`   | Dependency injection registration helpers                                                       |

### Database bootstrap

The shared `Sql/init.sql` script creates everything the examples need (schemas `Users` and `Types`, tables `Users.Users`
and `Users.Orders`, TVP types `Types.tvp_*` and 9 stored procedures). It is **idempotent** — every object is dropped
before being re-created.

- The **Default Console** runs `ExampleDatabaseBootstrapper.EnsureCreatedAsync(connectionString)` on startup.
- The **Aspire AppHost** hands the script to Aspire via `WithCreationScript(...)`, which runs it once on container
  provisioning.

## Demo scenarios

Both `Program.cs` files run the same scenario sequence so you can compare the two hosting models side-by-side. Each
scenario maps to specific telemetry signals emitted under the `CaeriusNet` `ActivitySource` / `Meter`:

| # | Scenario                                             | Repository method                    | Stored procedure                         | Notable telemetry                                                        |
|---|------------------------------------------------------|--------------------------------------|------------------------------------------|--------------------------------------------------------------------------|
| 1 | Single result-set + caches (frozen / memory / Redis) | `GetAllUsers*`                       | `Users.usp_Get_All_Users`                | `caerius.cache.lookups{hit,tier}`; cache HIT skips the DB span           |
| 2 | TVP-driven reads                                     | `GetUsersByTvp*`                     | `Users.usp_Get_Users_From_Tvp*`          | `caerius.tvp.used = true`, `caerius.tvp.type_name = Types.tvp_*`         |
| 3 | Multi result-set                                     | `GetDashboardAsync`                  | `Users.usp_Get_Dashboard`                | `caerius.resultset.multi = true`, `caerius.resultset.expected_count = 3` |
| 4 | TVP **+** multi result-set                           | `GetUsersWithOrdersByTvpAsync`       | `Users.usp_Get_Users_With_Orders_By_Tvp` | both TVP and multi-RS tags on a single span                              |
| 5 | Transaction commit                                   | `CreateUserWithFirstOrderAsync`      | `usp_Create_User` + `usp_Create_Order`   | `caerius.tx = true` on every command inside the transaction              |
| 6 | C#-side rollback                                     | `DemonstrateClientSideRollbackAsync` | `usp_Create_User`                        | success spans tagged `caerius.tx = true`; nothing persisted              |
| 7 | SQL-side rollback (`BEGIN CATCH` + `THROW`)          | `DemonstrateServerSideRollbackAsync` | `usp_Create_User_Tx_Safe`                | error span (`ActivityStatusCode.Error`) + `caerius.sp.errors` counter    |

For the full attribute reference,
see [Documentations/docs/documentation/aspire.md](../Documentations/docs/documentation/aspire.md#tracing--telemetry).

## Project Structure

```text
Exemples/
├── Aspire/
│   ├── CaeriusNet.Exemples.Aspire.AppHost/
│   ├── CaeriusNet.Exemples.Aspire.Console/
│   └── CaeriusNet.Exemples.Aspire.ServiceDefaults/
├── Default/
│   └── CaeriusNet.Exemples.Default.Console/
└── Libs/
    └── CaeriusNet.Exemples.Libs.Commons/
```
