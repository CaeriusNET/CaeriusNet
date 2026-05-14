# CAERIUS206 — OUTPUT parameters are not supported

**Severity**: Error
**Category**: CaeriusNet.AutoContracts

## Cause

The stored procedure declares one or more `OUTPUT` parameters. AutoContracts currently generates
read-only stored procedure contracts from input parameters and result sets; `OUTPUT` parameters are
not part of that generated surface.

## How to fix

Return values through the first result set instead of `OUTPUT` parameters. AutoContracts scans the
application schemas in the selected database, so a procedure with `OUTPUT` parameters in that
database is considered an incompatible contract until it is changed or moved out of that database's
generated contract boundary.

After changing the procedure contract, run `Pull` and review the refreshed
`caerius.contracts.json`.
