# CAERIUS002 — Type must be `partial`

**Severity**: Error
**Category**: CaeriusNet.Generator
**Applies to**: `[GenerateDto]`, `[GenerateTvp]`

## Cause

A type decorated with `[GenerateDto]` or `[GenerateTvp]` is not declared `partial`, so the
source generator cannot extend it with the generated mapper.

## How to fix

Add the `partial` modifier:

```csharp
[GenerateDto]
public sealed partial record FooDto(int Id, string Name);
```

## See also

- [CAERIUS001 — Type must be sealed](./CAERIUS001)
- [CAERIUS003 — Type must declare a primary constructor](./CAERIUS003)
