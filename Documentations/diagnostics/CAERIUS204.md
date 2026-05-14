# CAERIUS204 — First projection cannot be determined

**Severity**: Error
**Category**: CaeriusNet.AutoContracts

## Cause

AutoContracts could not determine the first result projection returned by the stored procedure from
SQL Server metadata.

## How to fix

Make the first result shape explicit and stable. Avoid branches, temporary dynamic SQL, temporary
tables whose shape cannot be discovered, or early returns that prevent SQL Server metadata discovery
from identifying the first projection.

If the procedure is command-only, remove it from read-contract generation instead of trying to emit a
DTO contract.
