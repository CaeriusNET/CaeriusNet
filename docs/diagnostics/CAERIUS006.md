# CAERIUS006 — Generator target shape is not supported

**Severity**: Error
**Category**: CaeriusNet.Analyzer

## Cause

A generator target is generic/nested or has a shape that CaeriusNet cannot generate safely. Targets must be non-generic
top-level partial types. `[GenerateDto]` is a sealed reference type; `[GenerateTvp]` is either a sealed positional
record class or a readonly positional record struct. An ordinary class with a primary constructor is not a TVP target.

## Why

The source generators emit companion `partial` declarations that must be placed in a well-known
namespace without additional type parameters. Generic/nested types would require reproducing the enclosing hierarchy
and type parameters. Allowing other TVP shapes would make static SQL row metadata ambiguous. Neither is generated.

## How to fix

Move the type to the top level and remove any generic type parameters:

```csharp
// Before — nested type, not supported.
public class Container
{
    [GenerateDto]
    public sealed partial record FooDto(int Id, string Name);
}

// Before — generic type, not supported.
[GenerateDto]
public sealed partial record FooDto<T>(int Id, T Value);

// After — top-level, non-generic type.
[GenerateDto]
public sealed partial record FooDto(int Id, string Name);
```

For a value-type TVP, use `public readonly partial record struct FooTvp(int Id);`.
