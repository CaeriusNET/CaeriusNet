# CAERIUS200 — AutoContracts manifest is missing

**Severity**: Error
**Category**: CaeriusNet.AutoContracts

## Cause

The AutoContracts source generator did not receive a `caerius.contracts.json` manifest through
Roslyn `AdditionalFiles`.

The generator reads only `AdditionalFiles`; it does not connect to SQL Server during compilation.
The CLI/MSBuild phase is responsible for reading SQL Server metadata in read-only mode and creating
the manifest before compilation consumes it.

## How to fix

Ensure the manifest is generated and included in the project as an `AdditionalFiles` item.

With the MSBuild package, the default manifest path is `$(ProjectDir)caerius.contracts.json`.
Override `CaeriusContractsOutput` only when the file intentionally lives elsewhere.

When using the CLI directly, run `pull --output <path>` or place `caerius.contracts.json` in the
current working directory, then include that file in the consuming project.
