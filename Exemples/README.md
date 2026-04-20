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

| Folder          | Purpose                                                 |
|-----------------|---------------------------------------------------------|
| `Abstractions/` | Repository contracts                                    |
| `Models/`       | DTOs with `[GenerateDto]` and TVPs with `[GenerateTvp]` |
| `Repositories/` | Repository implementations built on CaeriusNet          |
| `Extensions/`   | Dependency injection registration helpers               |

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
