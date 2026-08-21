# AutoContracts

AutoContracts is a separate package in v12. Install `CaeriusNet.Contracts` in the project that owns generated contract code; this package alone supplies the discovery tool and `buildTransitive` targets.

```bash
dotnet add package CaeriusNet.Contracts --version 11.2.0
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

The preferred connection sources are `CaeriusContractsConnectionName` (normal configuration) and `CaeriusContractsConnectionStringEnv` (CI secret). The MSBuild integration deliberately has no raw connection-string property: use configuration, User Secrets, an environment variable, or explicit non-secret connection arguments.

## Manifest v3 and additional result sets

`caerius.contracts.json` is manifest v3. Discovery emits server major/version features, server/database identity, table types and columns, procedure parameters, exact facets/collations and the first result set. It uses SQL Server metadata views/DMVs and does **not** execute stored procedures. Parameters and columns have zero-based, continuous ordinals; a return value is `Int` at ordinal 0.

SQL Server can describe only a first result set statically. The one optional `caerius.contracts.overrides.json` may replace set 0 only if discovery reported `None` or `Undetermined`, then add result sets 1 through 9. Indices must be unique, continuous and zero-based; intermediate `None` sets are invalid. The contract hash covers parameters, directions, TVPs, facets, collations, all result sets and the aggregate result shape.

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
  "version": 3,
  "procedures": [{
    "schema": "dbo",
    "name": "ApplyDiscount",
    "parameters": [{ "ordinal": 3, "direction": "InputOutput" }],
    "resultSets": []
  }]
}
```

`Pull` uses an atomic LF-normalized write only when no selected procedure has an error. `Verify` reparses, normalizes and recomputes the table-type, result-set and aggregate procedure hashes from every relevant field (including CLR type and SQL facets); it never trusts a serialized `contractHash` field. A manual edit retaining a previous hash therefore fails. Invalid JSON, invalid v3 shapes and overrides for an unknown procedure are compiler diagnostics.

## Configuration reference

| Property | Default | Purpose |
| --- | --- | --- |
| `CaeriusContractsMode` | `Off` | `Off`, `Pull` or `Verify`. |
| `CaeriusContractsOutput` | `$(MSBuildProjectDirectory)\caerius.contracts.json` | Canonical manifest v3 path. |
| `CaeriusContractsOverrides` | `$(MSBuildProjectDirectory)\caerius.contracts.overrides.json` | Additional result-set declarations. |
| `CaeriusContractsConnectionName` | `DefaultConnection` | Named configuration connection. |
| `CaeriusContractsConnectionStringEnv` | empty | Environment variable containing a connection string. |
| `CaeriusContractsConfigurationEnvironment` | `DOTNET_ENVIRONMENT` when set | `appsettings.{environment}.json` suffix forwarded to discovery. |
| `CaeriusContractsUserSecretsId` | project `UserSecretsId` | Optional user-secrets id forwarded to discovery. |
| `CaeriusContractsTimeout` | `30` | Discovery command timeout in seconds. |
| `CaeriusSqlServer2025Types` | unset | Set to `Enabled` only for a v3 manifest pulled from SQL Server major 17 to generate native JSON/vector APIs. |

## SQL Server 2025 native types

The v12 JSON/vector support is explicit: set `CaeriusSqlServer2025Types=Enabled` and regenerate against a major-17 server. Native JSON maps to `string`/`SqlDbType.Json`; native float32 vector maps to `SqlVector<float>`/`SqlDbType.Vector`. It is available for parameters, outputs and results, not TVP columns. SQL Server 2022 keeps JSON as `nvarchar(max)`. Spatial, CLR UDT and every type without a faithful mapping are rejected rather than silently downgraded.

For upgrade steps from the bundled v11 workflow, see the [migration guide](https://github.com/CaeriusNET/CaeriusNet/blob/main/docs/MIGRATION-v11-v12.md).
