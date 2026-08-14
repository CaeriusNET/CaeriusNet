# CaeriusNet.Contracts

`CaeriusNet.Contracts` is the build-time AutoContracts package for CaeriusNet 12. It alone ships the SQL Server
metadata-discovery tool and the opt-in `buildTransitive` targets; installing the runtime does not run database discovery
during every build.

## Prerequisites

- .NET SDK 10.0.303, or a later .NET 10 patch
- SQL Server 2022 or SQL Server 2025
- Metadata permissions for system catalogs and `sys.dm_exec_describe_first_result_set_for_object`

```bash
dotnet add package CaeriusNet.Contracts --version 12.0.0
```

## Pull and verify deliberately

Targets are disabled by default. Use `Pull` to refresh the committed manifest and `Verify` in CI to detect drift.

```xml
<PropertyGroup>
  <CaeriusContractsMode>Pull</CaeriusContractsMode>
  <CaeriusContractsConnectionName>Orders</CaeriusContractsConnectionName>
</PropertyGroup>
```

```bash
dotnet build
```

The normal input is a configuration connection name; `CaeriusContractsConnectionStringEnv` is appropriate for CI.
`CaeriusContractsConnectionString` still works but is deprecated because it can expose a secret in an MSBuild log.

`caerius.contracts.json` is manifest v2 and contains discovered server, database, TVP, parameter and first-result-set
metadata. SQL Server statically describes only a procedure's first result set, so declare sets 2–10 in
`caerius.contracts.overrides.json`. Override indices are continuous, unique and zero-based. No stored procedure is
executed during discovery.

SQL Server metadata marks an output-capable parameter but does not expose whether a caller must provide an initial
value. `Pull` therefore emits the safe `Output` direction. Declare a deliberately caller-supplied value as `InputOutput`
in the overrides file; this remains metadata-only and keeps that API decision under source control.

```json
{
  "version": 2,
  "procedures": [{
    "schema": "dbo",
    "name": "ApplyDiscount",
    "parameters": [{ "ordinal": 3, "direction": "InputOutput" }],
    "resultSets": []
  }]
}
```

For a named connection, `CaeriusContractsConfigurationEnvironment` and `CaeriusContractsUserSecretsId` are forwarded to
the tool. The defaults are `DOTNET_ENVIRONMENT` (when present) and the project’s `UserSecretsId`, respectively.

```xml
<PropertyGroup>
  <CaeriusContractsMode>Verify</CaeriusContractsMode>
  <CaeriusContractsConnectionStringEnv>CAERIUS_SQL_CONNECTION</CaeriusContractsConnectionStringEnv>
</PropertyGroup>
```

[Documentation](https://caerius.net) · [Migration guide](https://github.com/CaeriusNET/CaeriusNet/blob/main/MIGRATION-v11-v12.md) · [Issues](https://github.com/CaeriusNET/CaeriusNet/issues)
