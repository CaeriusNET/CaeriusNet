# CAERIUS003 — Type must declare a primary constructor

**Severity**: Error
**Category**: CaeriusNet.Generator

## Cause

A type decorated with `[GenerateDto]` or `[GenerateTvp]` does not declare a primary constructor.
The generated mapper relies exclusively on the ordered parameters of the primary constructor to
materialise instances — initialisers, properties or secondary constructors are ignored on purpose
to keep the SQL column / object-graph mapping deterministic.

## How to fix

Declare a primary constructor whose parameters mirror the SQL result-set columns (DTO) or the
TVP column ordering (TVP):

```csharp
[GenerateDto]
public sealed partial record FooDto(int Id, string Name, DateTime CreatedAt);
```
