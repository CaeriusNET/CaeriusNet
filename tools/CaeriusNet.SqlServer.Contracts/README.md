# CaeriusNet SQL Server AutoContracts tool

This executable is an implementation detail of the `CaeriusNet.Contracts` 12.0.0 package. It discovers SQL Server
2022/2025 **metadata only** and writes the v2 AutoContracts manifest consumed by the CaeriusNet source generator. It is
not installed from NuGet as a separate tool package.

Install the package that owns the opt-in MSBuild integration:

```bash
dotnet add package CaeriusNet.Contracts --version 12.0.0
```

Use a named connection locally and an environment-variable name in CI. A raw `--connection-string` remains available for
direct troubleshooting but is deprecated because shell and build logs can expose it.

```bash
dotnet CaeriusNet.SqlServer.Contracts.dll pull \
  --connection-name Orders \
  --configuration-base-path . \
  --configuration-environment Development \
  --output caerius.contracts.json

dotnet CaeriusNet.SqlServer.Contracts.dll verify \
  --connection-env CAERIUS_SQL_CONNECTION \
  --manifest caerius.contracts.json
```

`Pull` and `Verify` execute one read-only metadata command. They never execute a user stored procedure and never read
table data. Discovery includes server/database identity, table types and columns, procedures and parameters, and SQL
Server’s first-result-set description. SQL Server cannot statically describe later result sets, so declare result sets
2–10 in `caerius.contracts.overrides.json`.

SQL Server’s catalog likewise describes an output-capable parameter but cannot distinguish `Output` from caller-supplied
`InputOutput`. The generated v2 manifest chooses `Output`. When a procedure intentionally requires an input value and
returns its replacement, make that decision explicitly in the override file without executing it:

```json
{
  "version": 2,
  "procedures": [
    {
      "schema": "dbo",
      "name": "ApplyDiscount",
      "parameters": [
        { "ordinal": 3, "direction": "InputOutput" }
      ],
      "resultSets": []
    }
  ]
}
```

The `CaeriusNet.Contracts` README documents the supported MSBuild properties including
`CaeriusContractsConfigurationEnvironment` and `CaeriusContractsUserSecretsId`.
