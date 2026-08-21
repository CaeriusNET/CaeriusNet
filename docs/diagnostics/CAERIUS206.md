# CAERIUS206 — Parameter direction is not supported

**Severity**: Error
**Category**: CaeriusNet.AutoContracts

## Cause

The manifest declares an invalid direction, or a direction/type combination that cannot be generated safely. v12
AutoContracts supports `Input`, `Output`, `InputOutput`, and `ReturnValue`; table-valued parameters are always
`Input` only. A return value must be `SqlDbType.Int` at ordinal 0.

## How to fix

Correct the direction in the SQL contract or its permitted v3 override. Generated output records expose `Output`,
`InputOutput`, and `ReturnValue` only after the reader has been consumed and closed. Do not change a TVP to output or
input/output: SQL Server TVPs are input-only.

After changing the procedure contract, run `Pull` and review the refreshed
`caerius.contracts.json`.
