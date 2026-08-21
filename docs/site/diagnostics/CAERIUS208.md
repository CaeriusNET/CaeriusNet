# CAERIUS208 — Nullable result column

**Severity**: Warning
**Category**: CaeriusNet.AutoContracts

## Cause

SQL Server metadata marks a projected column as nullable. AutoContracts emits the corresponding nullable CLR member
and reports the warning so nullability is reviewed as part of the stored-procedure contract.

## How to fix

Make the projection non-nullable if that was the database contract, or handle the generated nullable CLR member if
null is intentional. An organization may promote this warning to an error in `.editorconfig`.

When nullability changes are intentional, refresh `caerius.contracts.json` with `Pull` and review the
generated nullable CLR type before committing.
