# CAERIUS006 — Generator target shape is not supported

**Severity**: Error
**Category**: CaeriusNet.Analyzer

## Cause

A generator target is generic/nested or has a shape that CaeriusNet cannot generate safely. Targets must be non-generic
top-level partial types. `[GenerateDto]` is a sealed reference type; `[GenerateTvp]` is either a sealed positional
record class or a readonly positional record struct. An ordinary class with a primary constructor is not a TVP target.

## Why

The source generators emit companion `partial` declarations that must be placed in a well-known
namespace without additional type parameters. Generic and nested types would require reproducing
the enclosing type hierarchy and all type parameters in the generated output, which is not
currently supported.

## How to fix

Move the type to the top level and remove any generic type parameters. Use a sealed positional record class or readonly
positional record struct for a TVP.
