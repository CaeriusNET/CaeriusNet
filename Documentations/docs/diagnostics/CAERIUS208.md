# CAERIUS208 — Nullable column violates policy

**Severity**: Policy-dependent
**Category**: CaeriusNet.AutoContracts

## Cause

SQL Server metadata marks a projected column as nullable, and the configured AutoContracts
nullability policy treats that column as invalid.

## How to fix

Make the projection non-nullable, adjust the contract to accept null values, or change the
nullability policy if nullable columns are intentional.

When nullability changes are intentional, refresh `caerius.contracts.json` with `Pull` and review the
generated nullable CLR type before committing.
