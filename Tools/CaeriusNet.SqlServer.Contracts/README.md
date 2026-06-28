# AutoContracts tool

This project builds the internal AutoContracts SQL Server discovery tool that ships inside the main `CaeriusNet` NuGet
package.

It is not a standalone public package. Consumers install only `CaeriusNet`; the package imports the MSBuild targets and
invokes this tool from its embedded `tools/net10.0/any` assets during normal `dotnet build` commands.

## Consumer usage

Add the main package to the project that owns your CaeriusNet data-access code.

```bash
dotnet add package CaeriusNet
```

## Pull contracts

Use `Pull` when you intentionally change stored procedure contracts.

```xml
<PropertyGroup>
  <CaeriusContractsMode>Pull</CaeriusContractsMode>
  <CaeriusContractsConnectionName>DefaultConnection</CaeriusContractsConnectionName>
</PropertyGroup>
```

```bash
dotnet build
```

The package reads `ConnectionStrings:DefaultConnection` from .NET configuration and writes:

```text
caerius.contracts.json
```

Commit that file with the application code that depends on it.

## Verify contracts

Use `Verify` in CI or release validation.

```xml
<PropertyGroup>
  <CaeriusContractsMode>Verify</CaeriusContractsMode>
  <CaeriusContractsConnectionStringEnv>SQLSERVER_CONNECTION_STRING</CaeriusContractsConnectionStringEnv>
</PropertyGroup>
```

```bash
dotnet build --configuration Release
```

If SQL Server metadata no longer matches `caerius.contracts.json`, the build fails with AutoContracts diagnostics.

## Configuration

| Property                                   | Default                               | Purpose                                                              |
|--------------------------------------------|---------------------------------------|----------------------------------------------------------------------|
| `CaeriusContractsMode`                     | `Off`                                 | Enables `Pull`, `Verify`, or disables AutoContracts.                 |
| `CaeriusContractsOutput`                   | `$(ProjectDir)caerius.contracts.json` | Manifest path.                                                       |
| `CaeriusContractsConnectionName`           | `DefaultConnection`                   | Named connection string from .NET configuration.                     |
| `CaeriusContractsConnectionStringEnv`      | Empty                                 | Environment variable that contains the SQL Server connection string. |
| `CaeriusContractsConnectionString`         | Empty                                 | Inline connection string. Prefer configuration or secrets instead.   |
| `CaeriusContractsConfigurationBasePath`    | `$(MSBuildProjectDirectory)`          | Directory used to load configuration files.                          |
| `CaeriusContractsConfigurationEnvironment` | Empty                                 | Environment suffix for `appsettings.{environment}.json`.             |
| `CaeriusContractsUserSecretsId`            | Project `UserSecretsId`               | User secrets ID override.                                            |

## Read-only behavior

AutoContracts reads SQL Server metadata only. It does not create, update, or delete database objects, and it does not
inspect table data.
