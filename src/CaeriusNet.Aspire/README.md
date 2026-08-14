# CaeriusNet.Aspire

`CaeriusNet.Aspire` wires CaeriusNet 12 to Aspire's SQL Server and Redis client integrations. It preserves Aspire
configuration and health behavior, creates a scope and connection lease per operation, and emits one Caerius client span
by default instead of duplicate low-level SqlClient spans.

## Prerequisites

- .NET 10 and C# 14
- `CaeriusNet` 12.0.0
- SQL Server 2022 (major version 16) or SQL Server 2025 (major version 17)

```bash
dotnet add package CaeriusNet.Aspire --version 12.0.0
```

## Configure an Aspire host

```csharp
using CaeriusNet.Aspire;

builder.AddCaeriusNetAspire(
    sqlConnectionName: "orders",
    configureCaerius: options => options.ApplicationNamespace = "shop",
    redisConnectionName: "cache");
```

For another database, use keyed DI:

```csharp
builder.AddKeyedCaeriusNetAspire(
    caeriusName: "reporting",
    sqlConnectionName: "reporting");

public sealed class Reports(
    [FromKeyedServices("reporting")] ICaeriusNetDbContext database)
{
}
```

The optional Caerius health check validates `SERVERPROPERTY('ProductMajorVersion')` once at startup and accepts only 16
or 17. Enable low-level SqlClient tracing only when you intentionally want those spans in addition to Caerius telemetry.

[Documentation](https://caerius.net) · [Migration guide](https://github.com/CaeriusNET/CaeriusNet/blob/main/MIGRATION-v11-v12.md) · [Issues](https://github.com/CaeriusNET/CaeriusNet/issues)
