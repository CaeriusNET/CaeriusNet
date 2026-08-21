# CAERIUS001 — Type must be `sealed`

**Severity**: Error
**Category**: CaeriusNet.Analyzer

## Cause

A `[GenerateDto]` type, or a class decorated with `[GenerateTvp]`, is not declared `sealed`. A TVP record struct is
instead required to be `readonly` and is diagnosed as an unsupported target shape when it is not.

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

A valid TVP value type is `public readonly partial record struct FooTvp(int Id);`.
