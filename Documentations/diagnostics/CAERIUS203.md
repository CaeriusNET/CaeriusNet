# CAERIUS203 — TVP SQL type is not supported

**Severity**: Error
**Category**: CaeriusNet.AutoContracts

## Cause

A column in a referenced table-valued parameter uses a SQL Server type that AutoContracts does not
support for generated contracts. The type may be valid SQL Server, but it cannot be represented
safely by the generated TVP mapper.

## How to fix

Change the TVP column to a supported SQL Server type, expose a supported boundary type, or exclude
the procedure until the type can be represented safely. Then refresh the manifest with `Pull`.
