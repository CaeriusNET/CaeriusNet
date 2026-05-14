# CAERIUS201 — Stored procedure could not be found

**Severity**: Error
**Category**: CaeriusNet.AutoContracts

## Cause

The manifest references a stored procedure that does not exist in the inspected SQL Server database.
This is detected by the CLI/MSBuild SQL Server metadata read, not by the Roslyn generator alone.

## How to fix

Check the procedure schema and name in `caerius.contracts.json`, then run `Verify` against the same
database shape expected by the application.

If the procedure was intentionally renamed or removed, run `Pull` to refresh the manifest and review
the generated contract changes.
