# CAERIUS007 — Manual TVP column metadata is invalid

**Severity**: Error
**Category**: CaeriusNet.Analyzer

## Cause

A manually generated TVP does not put exactly one `SqlServerTvpColumnAttribute` on each positional parameter, or its
SQL name, zero-based ordinal, `SqlDbType`, nullability or applicable facets are not faithful to the SQL Server table
type.

## How to fix

Use `AutoContracts` where possible. For a manual TVP, decorate every positional parameter and state required length,
precision, scale, LCID and compare options explicitly. Keep ordinals continuous from zero and equal to the user-defined
table type's order. Generation stops until the contract is complete.
