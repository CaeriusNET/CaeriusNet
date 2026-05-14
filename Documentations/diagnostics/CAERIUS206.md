# CAERIUS206 — OUTPUT parameters are not supported

**Severity**: Error
**Category**: CaeriusNet.AutoContracts

## Cause

The stored procedure declares one or more `OUTPUT` parameters. AutoContracts currently generates
read-only stored procedure contracts from input parameters and result sets; `OUTPUT` parameters are
not part of that generated surface.

## How to fix

Return values through the result set instead of `OUTPUT` parameters, or keep the procedure outside
AutoContracts read-only contract generation.

After changing the procedure contract, run `Pull` and review the refreshed
`caerius.contracts.json`.
