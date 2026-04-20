# CAERIUS002 — Type must be `partial`

**Severity**: Error
**Category**: CaeriusNet.Generator

## Cause

A type decorated with `[GenerateDto]` or `[GenerateTvp]` is not declared `partial`, so the
source generator cannot extend it with the generated mapper.

## How to fix

Add the `partial` modifier:

```csharp
[GenerateDto]
public sealed partial record FooDto(int Id, string Name);
```
