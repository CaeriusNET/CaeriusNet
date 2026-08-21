# CAERIUS007 — Manual TVP column metadata is invalid

**Severity**: Error
**Applies to**: manual `[GenerateTvp]` contracts

Every positional parameter of a manual TVP needs exactly one `SqlServerTvpColumnAttribute`. Its SQL name, zero-based
ordinal, `SqlDbType`, nullability and relevant length/precision/scale/collation facets must faithfully match the SQL
Server table type. Use AutoContracts where possible; otherwise correct the attribute values and continuous ordinal
sequence. No source is emitted until the contract is complete.
