# CAERIUS005 — CLR type has no faithful SQL Server mapping

**Severity**: Error
**Category**: CaeriusNet.Analyzer

## Cause

A parameter on a `[GenerateDto]` or `[GenerateTvp]` type uses a CLR type that the generator cannot map faithfully to a
native SQL Server type. Emission for that input stops; CaeriusNet never falls back to `SqlDbType.Variant`
(`sql_variant`).

Common offenders: `System.Uri`, `System.Version`, custom value objects, `System.Net.IPAddress`,
arbitrary reference types, etc.

## Why it matters

The fallback was unsafe because it silently changed the stored-procedure boundary, boxed values and lost faithful SQL
facets. v12 requires an explicit, supported contract instead.

## How to fix

Replace the offending property with a natively-mapped CLR type, or expose a conversion at the
DTO/TVP boundary:

```csharp
// Before — unsupported and no source is emitted.
[GenerateDto]
public sealed partial record EndpointDto(int Id, System.Uri Endpoint);

// After — explicit string mapping; SQL nvarchar works with all features.
[GenerateDto]
public sealed partial record EndpointDto(int Id, string Endpoint);
```

Use a supported CLR/SQL boundary type or write a dedicated stored-procedure boundary conversion outside generated DTO/
TVP code. `CAERIUS005` is a blocking contract diagnostic; suppressing it does not enable `sql_variant` generation.
