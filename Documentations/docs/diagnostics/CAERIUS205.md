# CAERIUS205 — Stored procedure has no result set

**Severity**: Warning
**Category**: CaeriusNet.AutoContracts

## Cause

The stored procedure was included in AutoContracts read-contract generation, but SQL Server metadata
reported no result set.

## How to fix

Return a stable result set from the procedure, or remove it from AutoContracts read-only contract
generation if it is command-only.

Use `Verify` in validation to catch accidental changes where a read procedure stops exposing its
first projection.
