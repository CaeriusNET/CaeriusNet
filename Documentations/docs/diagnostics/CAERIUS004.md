# CAERIUS004 — `TvpName` must not be empty

**Severity**: Error
**Category**: CaeriusNet.Generator
**Applies to**: `[GenerateTvp]`

## Cause

`[GenerateTvp(TvpName = "")]` (or any whitespace-only value) is not a valid SQL Server type name.
The generator needs a non-empty identifier to emit the `SqlParameter.TypeName` value.

## How to fix

Provide the fully-qualified TVP type name as configured in your SQL Server schema:

```csharp
[GenerateTvp(TvpName = "tvp_FooBar", Schema = "dbo")]
public sealed partial record FooBarTvp(int Id, string Name);
```

The `Schema` argument is optional and defaults to `dbo`.

## See also

- [Table-Valued Parameters](/documentation/tvp)
- [Source Generators — GenerateTvp](/documentation/source-generators#generatetvp-tvp-mapper)
