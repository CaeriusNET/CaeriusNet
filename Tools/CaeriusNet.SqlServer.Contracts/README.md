# CaeriusNet.SqlServer.Contracts

Build-time SQL Server contract discovery package for CaeriusNet AutoContracts.

The package contributes MSBuild `buildTransitive` imports and an embedded CLI used to pull or verify
`caerius.contracts.json` from SQL Server metadata. The source generator itself reads only `AdditionalFiles` and does not
connect to SQL Server.

## Connection strings

The preferred project setup is to resolve the SQL Server connection by .NET configuration name:

```xml
<PropertyGroup>
  <CaeriusContractsMode>Pull</CaeriusContractsMode>
  <CaeriusContractsConnectionName>DefaultConnection</CaeriusContractsConnectionName>
</PropertyGroup>
```

The embedded CLI reads `ConnectionStrings:DefaultConnection` from `appsettings.json`,
`appsettings.{environment}.json`, user secrets, and environment variables. This matches manual
configuration and Aspire-provided `ConnectionStrings__DefaultConnection` values.

The database is selected by the SQL Server connection string. AutoContracts scans all application
schemas in that database, including `dbo`, and ignores SQL Server system schemas such as `sys` and
`INFORMATION_SCHEMA`.

Direct CLI usage:

```powershell
dotnet CaeriusNet.SqlServer.Contracts.dll pull `
  --connection-name DefaultConnection `
  --configuration-base-path . `
  --output caerius.contracts.json
```

`--connection-env` and `--connection-string` remain available for CI and explicit scripting.
