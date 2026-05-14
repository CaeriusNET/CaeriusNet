# AutoContracts

AutoContracts keeps CaeriusNet stored procedure contracts aligned with SQL Server metadata. It is built into the `CaeriusNet` NuGet package. You install one package, configure MSBuild properties, and run `dotnet build`.

Use AutoContracts when stored procedures are deployed separately from application code, reviewed by another team, or validated in CI before release.

## Install CaeriusNet

Install the main package in the project that owns your CaeriusNet data-access code.

```bash
dotnet add package CaeriusNet
```

No additional package is required. `CaeriusNet` includes the runtime, analyzers, source generators, build integration, and SQL Server contract discovery support.

## How AutoContracts fits in your build

AutoContracts uses normal `dotnet build` commands.

1. Set `CaeriusContractsMode` in your project file.
2. Build the project.
3. In `Pull` mode, CaeriusNet reads SQL Server metadata and writes `caerius.contracts.json`.
4. In `Verify` mode, CaeriusNet compares SQL Server metadata with the committed manifest.
5. The compiler uses the manifest to emit typed contract helpers for procedures, parameters, result rows, and table-valued parameters.

The default manifest path is:

```xml
$(ProjectDir)caerius.contracts.json
```

Commit this file with the code that depends on it.

## Configure AutoContracts

Add the MSBuild properties to your application `.csproj`.

```xml
<PropertyGroup>
  <CaeriusContractsMode>Pull</CaeriusContractsMode>
  <CaeriusContractsConnectionName>DefaultConnection</CaeriusContractsConnectionName>
</PropertyGroup>
```

Then build the project:

```bash
dotnet build
```

AutoContracts resolves `ConnectionStrings:DefaultConnection` from standard .NET configuration sources:

- `appsettings.json`
- `appsettings.{environment}.json`
- User secrets
- Environment variables such as `ConnectionStrings__DefaultConnection`

For Aspire projects, use the same connection-string name that your app uses.

## Modes

Set `CaeriusContractsMode` to one of these values.

| Mode | Build behavior |
|---|---|
| `Off` | Does nothing. This is the default. |
| `Pull` | Reads SQL Server metadata and refreshes `caerius.contracts.json` before compilation. |
| `Verify` | Reads SQL Server metadata and fails the build if the database no longer matches `caerius.contracts.json`. |

Use `Pull` during intentional database contract changes. Use `Verify` in CI and release validation.

## Pull contracts

Use `Pull` when you add or change stored procedures, table-valued parameter types, parameters, or result shapes.

```xml
<PropertyGroup>
  <CaeriusContractsMode>Pull</CaeriusContractsMode>
  <CaeriusContractsConnectionName>DefaultConnection</CaeriusContractsConnectionName>
</PropertyGroup>
```

```bash
dotnet build
```

Review the updated `caerius.contracts.json`, then commit it with the related C# and SQL changes.

## Verify contracts in CI

Use `Verify` when the manifest already exists and the build should fail on contract drift.

```xml
<PropertyGroup>
  <CaeriusContractsMode>Verify</CaeriusContractsMode>
  <CaeriusContractsConnectionName>DefaultConnection</CaeriusContractsConnectionName>
</PropertyGroup>
```

```bash
dotnet build --configuration Release
```

If SQL Server metadata differs from the manifest, AutoContracts reports diagnostics such as `CAERIUS201` through `CAERIUS210`.

## Use a CI connection string

CI systems usually provide connection strings through environment variables. Configure the property once:

```xml
<PropertyGroup>
  <CaeriusContractsMode>Verify</CaeriusContractsMode>
  <CaeriusContractsConnectionStringEnv>SQLSERVER_CONNECTION_STRING</CaeriusContractsConnectionStringEnv>
</PropertyGroup>
```

Then set `SQLSERVER_CONNECTION_STRING` in your CI secret store.

## Override the manifest path

Use the default path unless your project has a specific layout requirement.

```xml
<PropertyGroup>
  <CaeriusContractsOutput>Contracts\caerius.contracts.json</CaeriusContractsOutput>
</PropertyGroup>
```

CaeriusNet automatically supplies that file to the compiler when it exists.

## Use generated contracts

After `caerius.contracts.json` exists, CaeriusNet generates typed helpers from the stored procedure metadata.

```csharp
ReadOnlyMemory<CustomerIdRowsTvp> ids = new[]
{
    new CustomerIdRowsTvp(1),
    new CustomerIdRowsTvp(2)
};

var parameters = new CustomerSearchParameters(ids, IncludeDisabled: false);

var storedProcedure = StoredProcedureParametersBuilder<CustomerSearchProcedure>
    .Create(parameters, resultSetCapacity: ids.Length)
    .Build();

var results = await dbContext.QueryAsReadOnlyCollectionAsync<CustomerSearchResult>(
    storedProcedure,
    cancellationToken);
```

The generated names come from the manifest produced by `Pull`. Review the manifest when database objects are renamed so the generated API remains clear.

## Configuration reference

| Property | Default | Purpose |
|---|---|---|
| `CaeriusContractsMode` | `Off` | Enables `Pull`, `Verify`, or disables AutoContracts. |
| `CaeriusContractsOutput` | `$(ProjectDir)caerius.contracts.json` | Manifest path. |
| `CaeriusContractsConnectionName` | `DefaultConnection` | Named connection string from .NET configuration. |
| `CaeriusContractsConnectionStringEnv` | Empty | Environment variable that contains the SQL Server connection string. |
| `CaeriusContractsConnectionString` | Empty | Inline connection string. Prefer configuration or secrets instead. |
| `CaeriusContractsConfigurationBasePath` | `$(MSBuildProjectDirectory)` | Directory used to load configuration files. |
| `CaeriusContractsConfigurationEnvironment` | Empty | Environment suffix for `appsettings.{environment}.json`. |
| `CaeriusContractsUserSecretsId` | Project `UserSecretsId` | User secrets ID override. |

## Recommended workflow

1. Install `CaeriusNet`.
2. Set `CaeriusContractsMode` to `Pull`.
3. Run `dotnet build` to create or refresh `caerius.contracts.json`.
4. Review and commit the manifest.
5. Change CI to `CaeriusContractsMode=Verify`.
6. Let CaeriusNet validate and generate contracts during normal builds.

## Read-only guarantees

AutoContracts reads SQL Server metadata only.

- It does not create, update, or delete SQL Server objects.
- It does not inspect table data.
- `Pull` writes only the manifest file in your project.
- `Verify` reports drift and leaves both SQL Server and the manifest unchanged.
