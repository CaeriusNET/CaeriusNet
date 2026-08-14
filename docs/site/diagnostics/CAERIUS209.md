# CAERIUS209 — Contract hash differs during Verify

**Severity**: Error
**Category**: CaeriusNet.AutoContracts

## Cause

`Verify` computed a contract hash from current SQL Server metadata that differs from the hash stored
in `caerius.contracts.json`.

## How to fix

If the database change is intentional, run `Pull`, review the updated `caerius.contracts.json`, and
commit it with the application change.

If the change is not intentional, restore the expected SQL Server contract and rerun `Verify`. The
verification path is read-only: it reports the mismatch but does not update SQL Server or the
manifest.
