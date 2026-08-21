# CaeriusNet SQL Server AutoContracts tool

This executable is an implementation detail of the `CaeriusNet.Contracts` 11.2.0 package. It discovers SQL Server
2022/2025 **metadata only** and writes the canonical v3 AutoContracts manifest consumed by the CaeriusNet source
generator. It is not installed from NuGet as a separate tool package.

Install the package that owns the opt-in MSBuild integration:

```bash
dotnet add package CaeriusNet.Contracts --version 11.2.0
```

Use a named connection locally and an environment-variable name in CI. A raw `--connection-string` remains available for
direct troubleshooting but is deprecated because shell logs can expose it; the MSBuild integration intentionally never
accepts a raw connection string. Errors redact connection values.

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

`Pull` and `Verify` execute read-only metadata commands. They never execute a user stored procedure and never read table
data. Discovery includes server/database identity, server major/features, table types and columns, procedures and
parameters, and SQL Server’s first-result-set description. `--include`/`--exclude` are applied before expensive result
metadata discovery, and only TVPs used by selected procedures are discovered.

`Pull` writes atomically only after every selected contract validates, with LF line endings. `Verify` reparses,
normalizes and recomputes every semantic hash; a manual edit retaining a previous hash fails. SQL Server cannot
statically describe later result sets, dynamic SQL or `#temp` results, so declare result sets 2–10 in the one optional
`caerius.contracts.overrides.json` file. It may replace set 0 only when that set was `None`/`Undetermined`, then add
continuous sets 1–9.

SQL Server’s catalog likewise describes an output-capable parameter but cannot distinguish `Output` from caller-supplied
`InputOutput`. The generated v3 manifest chooses `Output`. When a procedure intentionally requires an input value and
returns its replacement, make that decision explicitly in the override file without executing it:

```json
{
  "version": 3,
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
