# CAERIUS006 — Generator target shape is not supported

**Severity**: Error
**Category**: CaeriusNet.Generator

## Cause

A type decorated with `[GenerateDto]` or `[GenerateTvp]` is either a generic type or a nested
type. CaeriusNet generators currently only support non-generic, top-level types.

## Why

The source generators emit companion `partial` declarations that must be placed in a well-known
namespace without additional type parameters. Generic and nested types would require reproducing
the enclosing type hierarchy and all type parameters in the generated output, which is not
currently supported.

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
