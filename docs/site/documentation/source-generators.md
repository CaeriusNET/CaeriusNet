# Source generators and analyzer

CaeriusNet v12 keeps the source generator and analyzer as separate Roslyn assemblies. Both use the .NET 10 generation floor, Roslyn `Microsoft.CodeAnalysis.CSharp` 5.0.0 and C# language version 14.0, avoiding consumers' compiler-version mismatch.

## DTOs and TVPs

```csharp
[GenerateDto]
public sealed partial record Customer(int Id, string Name);

[GenerateTvp(Schema = "dbo", TvpName = "CustomerIdType")]
public sealed partial record CustomerId(int Value);
```

The DTO generator emits the static row mapper and a typed binary cache codec. The TVP generator emits private immutable metadata, a static row writer and empty-set support. Neither generator uses runtime reflection or clones TVP metadata for each call.

## Procedure overloads

The multi-result overload families are generated at build time for the runtime itself, not by a generator embedded into a consuming application. Consumers receive the complete 2–10 result-set surface without any assembly-name dependent behavior.

## AutoContracts

The AutoContracts generator reads manifest v2 files supplied as `AdditionalFiles` by `CaeriusNet.Contracts`. It shares JSON models, SQL/C# naming and validation rules with the analyzer through linked source files, not a runtime binary dependency.

For discovery configuration, see [AutoContracts](/documentation/auto-contracts).
