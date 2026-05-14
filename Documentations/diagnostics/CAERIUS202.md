# CAERIUS202 — Referenced TVP type is missing

**Severity**: Error
**Category**: CaeriusNet.AutoContracts

## Cause

A stored procedure parameter references a table-valued parameter type that was not found in the
inspected SQL Server database. AutoContracts needs that type metadata to generate the typed TVP
contract.

## How to fix

Create the expected user-defined table type, correct the schema-qualified TVP name, or update the
procedure so it references the intended type.

After an intentional database change, set `CaeriusContractsMode` to `Pull` and run `dotnet build` so `caerius.contracts.json` contains the current TVP shape.
