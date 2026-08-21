# Source generators and analyzer

CaeriusNet v12 keeps the source generator and analyzer as separate Roslyn assemblies. Both target `netstandard2.0`, run with the .NET SDK 10.0.400 floor, and pin `Microsoft.CodeAnalysis.CSharp`/analyzers 5.9.0. Consumers require Roslyn 5.9 or later and compile with C# language version 14.0, avoiding compiler-version mismatch.

## DTOs and TVPs

```csharp
[GenerateDto]
public sealed partial record Customer(int Id, string Name);

[GenerateTvp(Schema = "dbo", TvpName = "CustomerIdType")]
public sealed partial record CustomerId(int Value);
```

The DTO generator emits the static row mapper and a typed binary cache codec. The TVP generator emits private immutable metadata, a static row writer and empty-set support. `[GenerateTvp]` is valid only for a sealed positional record class or a readonly positional record struct; regular primary-constructor classes are rejected. Neither generator uses runtime reflection or clones TVP metadata for each call.

`[GenerateDto]` intentionally has no manual SQL length, precision, scale or collation attributes: a hand-authored DTO is a
CLR mapping contract. For a production SQL contract requiring exact database facets, collations and discovered metadata,
use AutoContracts v3. `SqlServerTvpColumnAttribute` applies only to manual TVP positional parameters.

Every SQL/CLR mapping is explicit. Unsupported input is an error with no source emission—there is no `sql_variant` fallback. `SqlDecimal` is used end-to-end for decimal precision 29–38, and codecs use strict UTF-8 plus hashes that include SQL type, facets and enum underlying type.

## Procedure overloads

The multi-result overload families are generated at build time for the runtime itself, not by a generator embedded into a consuming application. Consumers receive the complete 2–10 result-set surface without any assembly-name dependent behavior.

## AutoContracts

The AutoContracts generator reads one canonical manifest v3 and one optional override supplied as `AdditionalFiles` by `CaeriusNet.Contracts`. It shares JSON models, SQL/C# naming and validation rules with the analyzer through linked source files, not a runtime binary dependency. The parser caps each file at 8 MiB and JSON depth at 64; invalid input produces diagnostics without a generator exception or partial source emission.

Native SQL Server 2025 JSON/vector generation is off by default. It requires `CaeriusSqlServer2025Types=Enabled` and a v3 manifest from server major 17. JSON/vector TVP columns remain unsupported in v12.

For discovery configuration, see [AutoContracts](/documentation/auto-contracts).
