# AutoContracts

AutoContracts helps keep CaeriusNet call sites aligned with SQL Server stored procedure metadata. It reads stored procedure contracts from SQL Server, stores them in a local manifest, and can verify that the database still matches the contract your application expects.

Use AutoContracts when stored procedures are shared across teams, deployed separately from application code, or validated in CI.

## What AutoContracts checks

AutoContracts tracks the public contract shape used by typed stored procedure calls:

- Stored procedure schema and name
- Parameter names, SQL types, direction, and nullability
- Result-set columns when SQL Server can expose them through metadata
- Typed call contracts used by `StoredProcedureParametersBuilder<TProcedure>`

AutoContracts does not inspect data rows and does not execute business logic. It only reads SQL Server metadata.

## Manifest file

The contract snapshot is stored in `caerius.contracts.json`.

When AutoContracts runs through MSBuild, the default path is:

```xml
$(ProjectDir)caerius.contracts.json
```

Override `CaeriusContractsOutput` when a project needs a different manifest path:

```xml
<PropertyGroup>
  <CaeriusContractsOutput>Contracts\caerius.contracts.json</CaeriusContractsOutput>
</PropertyGroup>
```

When you run the CLI directly:

- `pull` writes to `--output` when provided, then `--manifest` when provided, and otherwise `caerius.contracts.json` in the current directory.
- `verify` reads from `--manifest` when provided, then `--output` when provided, and otherwise `caerius.contracts.json` in the current directory.

## Modes

Set `CaeriusContractsMode` to control how AutoContracts behaves.

| Mode | Behavior |
|---|---|
| `Pull` | Reads SQL Server metadata and refreshes `caerius.contracts.json`. Use this after an intentional stored procedure contract change. |
| `Verify` | Reads SQL Server metadata and compares it with `caerius.contracts.json`. Use this in validation workflows to detect drift. |
| `Off` | Skips AutoContracts. Use this when contract checks are not needed for a run. |

## Configure the connection string

Prefer resolving the discovery connection string by name so AutoContracts follows the same configuration model as your application.

```xml
<PropertyGroup>
  <CaeriusContractsMode>Pull</CaeriusContractsMode>
  <CaeriusContractsConnectionName>DefaultConnection</CaeriusContractsConnectionName>
</PropertyGroup>
```

AutoContracts reads `ConnectionStrings:DefaultConnection` from .NET configuration. It supports `appsettings.json`, optional `appsettings.{environment}.json`, user secrets, and environment variables from the project directory.

Use these properties when you need to control configuration resolution:

| Property | Purpose |
|---|---|
| `CaeriusContractsConfigurationEnvironment` | Selects the environment-specific configuration file. |
| `CaeriusContractsUserSecretsId` | Overrides the project `UserSecretsId`. |
| `CaeriusContractsConnectionName` | Selects the named connection string. |

For Aspire-driven projects, the same configuration path works with `ConnectionStrings__DefaultConnection`. For local manual databases, store the value in `appsettings.Development.json` or user secrets. Do not commit production secrets.

## CLI examples

Refresh a manifest from a named connection:

```powershell
dotnet run --project Tools/CaeriusNet.SqlServer.Contracts -- `
  pull `
  --connection-name DefaultConnection `
  --configuration-base-path .\src\MyApp `
  --output .\src\MyApp\caerius.contracts.json
```

Verify an existing manifest with a connection string from the environment:

```powershell
dotnet run --project Tools/CaeriusNet.SqlServer.Contracts -- `
  verify `
  --connection-env SQLSERVER_CONNECTION_STRING `
  --manifest .\src\MyApp\caerius.contracts.json
```

## Recommended workflow

1. Change the stored procedure contract intentionally.
2. Run AutoContracts with `Pull`.
3. Review the updated `caerius.contracts.json`.
4. Commit the application change and the refreshed contract snapshot together.
5. Run AutoContracts with `Verify` in CI to catch unexpected SQL Server drift.

## Read-only guarantees

AutoContracts uses SQL Server metadata reads only.

- `Pull` updates `caerius.contracts.json` in the application workspace. It does not apply SQL changes back to the database.
- `Verify` reports mismatches and leaves both SQL Server and the snapshot unchanged.
- `Off` disables the check for local scenarios where SQL Server is unavailable.

Prefer `Verify` wherever a real database is part of validation.
