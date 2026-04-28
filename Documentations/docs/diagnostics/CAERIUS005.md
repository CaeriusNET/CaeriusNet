# CAERIUS005 — Unmapped CLR type falls back to `sql_variant`

**Severity**: Warning
**Category**: CaeriusNet.Generator
**Applies to**: `[GenerateDto]`, `[GenerateTvp]`

## Cause

A parameter on a `[GenerateDto]` or `[GenerateTvp]` type uses a CLR type that the generator
cannot map to a native SQL Server data type. The generated code falls back to
`SqlDbType.Variant` (`sql_variant`).

Common offenders: `System.Uri`, `System.Version`, custom value objects, `System.Net.IPAddress`,
arbitrary reference types, etc.

## Why it matters

`sql_variant` works, but it disables several SQL Server features:

- No participation in indexes (no seek/scan optimizations).
- Cannot back computed columns.
- Cannot be referenced from `CHECK` constraints.
- Forces boxing/unboxing on every round-trip.
- ORM/tooling support is limited.

## How to fix

Replace the offending property with a natively-mapped CLR type, or expose a conversion at the
DTO/TVP boundary:

```csharp
// Before — emits sql_variant.
[GenerateDto]
public sealed partial record EndpointDto(int Id, System.Uri Endpoint);

// After — explicit string mapping; SQL nvarchar works with all features.
[GenerateDto]
public sealed partial record EndpointDto(int Id, string Endpoint);
```

If you intentionally want `sql_variant`, suppress the warning with
`#pragma warning disable CAERIUS005` around the declaration.

## See also

- [Compiler Diagnostics](/documentation/diagnostics#suppressing-diagnostics)
- [Table-Valued Parameters](/documentation/tvp)
