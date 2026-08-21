# CaeriusNet.Contracts

`CaeriusNet.Contracts` is the build-time AutoContracts package for CaeriusNet 12. It alone ships the SQL Server
metadata-discovery tool and the opt-in `buildTransitive` targets; installing the runtime does not run database discovery
during every build.

## Prerequisites

- .NET SDK 10.0.400, or a later .NET 10 patch accepted by `global.json`
- Roslyn 5.9 or later for a consumer that runs the generator
- SQL Server 2022 or SQL Server 2025
- Metadata permissions for system catalogs and `sys.dm_exec_describe_first_result_set_for_object`

```bash
dotnet add package CaeriusNet.Contracts --version 11.2.0
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
MSBuild deliberately has no raw connection-string property: use configuration, User Secrets, an environment variable, or
`CaeriusContractsConnectionArguments` without putting a secret in a project or build log.

`caerius.contracts.json` is the canonical **manifest v3**. It records server major version/features, schema-prefixed CLR
names, table types, parameters, exact facets/collations and ordered result sets. Parameter and column ordinals are
zero-based and continuous; a `ReturnValue`, if present, is `Int` at ordinal 0. `Pull` writes only after discovery and
validation have no errors, by atomically replacing the output with LF line endings. `Verify`
reparses, normalizes and recomputes semantic hashes, so a hand edit retaining an old hash fails.

SQL Server statically describes only the first result set. The one optional override file may replace result set 0 only
when discovery returned `None` or `Undetermined`, then add contiguous sets 1–9; it cannot create an intermediate
`None`. No stored procedure is executed during discovery.

SQL Server metadata marks an output-capable parameter but does not expose whether a caller must provide an initial
value. `Pull` therefore emits the safe `Output` direction. Declare a deliberately caller-supplied value as `InputOutput`
in the override file; this remains metadata-only and keeps that API decision under source control.

```json
{
  "version": 3,
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

## SQL Server 2025 native types

SQL Server 2025 native `json` and `vector` contracts are opt-in. Set
`<CaeriusSqlServer2025Types>Enabled</CaeriusSqlServer2025Types>` only with a v3 manifest pulled from a major-17 server.
Generated JSON parameters/results use `string` and `SqlDbType.Json`; float32 vectors use
`SqlVector<float>` and `SqlDbType.Vector`. The v12 opt-in supports parameters, outputs and result columns only—JSON and
vector TVP columns remain blocked until the provider matrix is validated. SQL Server 2022 continues to use JSON as
`nvarchar(max)`.

[Documentation](https://caerius.net) · [Migration guide](https://github.com/CaeriusNET/CaeriusNet/blob/main/docs/MIGRATION-v11-v12.md) · [Issues](https://github.com/CaeriusNET/CaeriusNet/issues)
