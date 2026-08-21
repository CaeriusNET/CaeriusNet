# CAERIUS207 — SQL type cannot be mapped

**Severity**: Error
**Category**: CaeriusNet.AutoContracts

## Cause

A procedure parameter or projected column uses a SQL Server type that AutoContracts cannot map to a
CLR contract type. The generated contract would otherwise expose an unsafe or ambiguous C# type.

## How to fix

Use a supported SQL Server type, cast the value in the first projection, or introduce a database
boundary shape that maps cleanly.

Run `Pull` after the SQL change so the manifest and generated contract agree.
