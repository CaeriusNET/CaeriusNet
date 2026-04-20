# CAERIUS001 — Type must be `sealed`

**Severity**: Error
**Category**: CaeriusNet.Generator

## Cause

A type decorated with `[GenerateDto]` or `[GenerateTvp]` is not declared `sealed`.

## Why

CaeriusNet's source generators emit per-type mappers that bypass virtual dispatch and rely on the
exact runtime layout. Allowing inheritance would break the assumption that the generated code can
materialise an instance using the primary constructor with no further polymorphism. Sealing the
type also gives the JIT additional optimisation room for the hot-path mapping.

## How to fix

Add the `sealed` modifier:

```csharp
[GenerateDto]
public sealed partial record FooDto(int Id, string Name);
```
