# AutoContracts

AutoContracts is a separate package in v12. Install `CaeriusNet.Contracts` in the project that owns generated contract code; this package alone supplies the discovery tool and `buildTransitive` targets.

```bash
dotnet add package CaeriusNet.Contracts --version 12.0.0
```

## Opt-in build modes

`Pull` and `Verify` do nothing unless selected. Use `Pull` when an intentional SQL contract change should update the manifest; use `Verify` in CI to reject drift.

```xml
<PropertyGroup>
  <CaeriusContractsMode>Pull</CaeriusContractsMode>
  <CaeriusContractsConnectionName>Orders</CaeriusContractsConnectionName>
</PropertyGroup>
```

```bash
dotnet build
```

The preferred connection sources are `CaeriusContractsConnectionName` (normal configuration) and `CaeriusContractsConnectionStringEnv` (CI secret). A raw `CaeriusContractsConnectionString` is supported only as a deprecated fallback because it can appear in MSBuild logs.

## Manifest v2 and additional result sets

`caerius.contracts.json` is manifest v2. Discovery emits server/database identity, table types and columns, procedure parameters and the first result set. It uses SQL Server metadata views/DMVs and does **not** execute stored procedures.

SQL Server can describe only a first result set statically. Declare result sets 2 through 10 in `caerius.contracts.overrides.json`; their indices must be unique, continuous and zero-based in the range 0–9. The contract hash covers parameters, directions, TVPs, all result sets and the aggregate result shape.

```xml
<PropertyGroup>
  <CaeriusContractsMode>Verify</CaeriusContractsMode>
  <CaeriusContractsConnectionStringEnv>CAERIUS_SQL_CONNECTION</CaeriusContractsConnectionStringEnv>
</PropertyGroup>
```

Commit both manifest files. Generated procedure helpers bind inputs, input/outputs, outputs and return values into the v12 `StoredProcedureCommandBuilder<TProcedure>` and expose typed output records after reader consumption.

`sys.parameters.is_output` identifies output-capable parameters but cannot distinguish a pure `OUTPUT` call from a caller-supplied `INPUTOUTPUT` value. `Pull` therefore emits `Output`. Declare the latter explicitly in the override file; discovery remains metadata-only and does not execute the procedure:

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

The generator recomputes the table-type, result-set and aggregate procedure hashes from every relevant field (including CLR type and SQL facets); it never trusts a serialized `contractHash` field. Invalid JSON, invalid v2 shapes and overrides for an unknown procedure are compiler diagnostics.

## Configuration reference

| Property | Default | Purpose |
| --- | --- | --- |
| `CaeriusContractsMode` | `Off` | `Off`, `Pull` or `Verify`. |
| `CaeriusContractsOutput` | `$(MSBuildProjectDirectory)\caerius.contracts.json` | Manifest v2 path. |
| `CaeriusContractsOverrides` | `$(MSBuildProjectDirectory)\caerius.contracts.overrides.json` | Additional result-set declarations. |
| `CaeriusContractsConnectionName` | `DefaultConnection` | Named configuration connection. |
| `CaeriusContractsConnectionStringEnv` | empty | Environment variable containing a connection string. |
| `CaeriusContractsConfigurationEnvironment` | `DOTNET_ENVIRONMENT` when set | `appsettings.{environment}.json` suffix forwarded to discovery. |
| `CaeriusContractsUserSecretsId` | project `UserSecretsId` | Optional user-secrets id forwarded to discovery. |
| `CaeriusContractsTimeout` | `30` | Discovery command timeout in seconds. |

For upgrade steps from the bundled v11 workflow, see the [migration guide](https://github.com/CaeriusNET/CaeriusNet/blob/main/MIGRATION-v11-v12.md).
