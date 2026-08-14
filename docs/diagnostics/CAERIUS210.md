# CAERIUS210 — Stored procedure is probably incompatible

**Severity**: Warning
**Category**: CaeriusNet.AutoContracts

## Cause

AutoContracts detected procedure metadata that is likely incompatible with read-only contract
generation, but the database did not expose enough detail for a more specific diagnostic.

## How to fix

Review the procedure shape for unsupported parameters, unsupported SQL types, dynamic SQL, temporary
objects, or branches that change the first result set.

Prefer making the first projection explicit and metadata-discoverable. If the procedure is not meant
to produce a typed read contract, remove it from AutoContracts generation.
