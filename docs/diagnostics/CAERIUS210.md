# CAERIUS210 — Procedure may be incompatible with generated contracts

**Severity**: Warning
**Category**: CaeriusNet.AutoContracts

## Cause

AutoContracts detected a procedure shape that may be incompatible with generated contracts, but SQL Server did not
expose enough information for a more specific diagnostic. Common cases are dynamic SQL, `#temp` tables, branch-dependent
projections, ambiguous first-result metadata, or an unsupported construct.

## How to fix

Review the procedure shape for unsupported parameters/types, dynamic SQL, temporary objects, or branches that change
the first result set.

Prefer making the first projection explicit and metadata-discoverable. For later/dynamic result sets, use the allowed
v3 override; otherwise exclude the procedure from AutoContracts generation.
