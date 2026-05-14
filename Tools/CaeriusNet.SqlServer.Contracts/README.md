# CaeriusNet.SqlServer.Contracts

Build-time SQL Server contract discovery package for CaeriusNet AutoContracts.

The package contributes MSBuild `buildTransitive` imports and an embedded CLI used to pull or verify
`caerius.contracts.json` from SQL Server metadata. The source generator itself reads only `AdditionalFiles` and does not
connect to SQL Server.
