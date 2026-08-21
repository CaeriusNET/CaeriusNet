# CaeriusNet source generators

CaeriusNet v12 uses incremental Roslyn generators to produce compile-time SQL mapping code for DTOs, TVPs and
AutoContracts. The public generator targets `netstandard2.0` so it can run inside the .NET 10 compiler, while its Roslyn
dependencies are pinned to `Microsoft.CodeAnalysis.CSharp` and analyzers 5.9.0. Consumers need SDK 10.0.400 (patch
roll-forward), Roslyn 5.9 or later, and C# 14.

The user-facing analyzer is a separate assembly. Both projects share source models, naming rules and validation logic;
neither has a binary dependency on the other. The analyzer reports usage diagnostics, while generators keep their
incremental pipeline deterministic and emit no code for invalid input.

## DTO and TVP generation

```csharp
using CaeriusNet;

[GenerateDto]
public sealed partial record Product(int Id, string Name);

[GenerateTvp(Schema = "dbo", TvpName = "ProductIdType")]
public sealed partial record ProductId(int Id);
```

For a DTO, the generator emits the static `ISpMapper<T>.MapFromDataReader` implementation, a deterministic binary
`ICaeriusCacheCodec<T>`, and an `IServiceCollection` registration extension. A DTO that uses only supported
SQL-compatible member types can therefore be used directly with `CaeriusNet.Redis` after registering its generated
codec.

For a TVP, the generator emits the static `ITvpMapper<T>` contract: SQL type name, immutable
`ImmutableArray<SqlMetaData>` metadata, `SqlDataRecord` factory and static row writer. The runtime reuses one record
while enumerating rows. It does not inspect a first element, clone metadata per call or reject an empty enumerable.

Constructor selection is symbol-based and deterministic. DTO types remain sealed partial reference types. TVP types are
only a sealed positional record class or a readonly positional record struct; a conventional class with a primary
constructor is rejected. A manual TVP must attach `SqlServerTvpColumnAttribute` to every positional parameter so all SQL
names, ordinals and facets are explicit.

Unsupported types are blocking diagnostics and produce no source: the generator never falls back to `sql_variant`.
`SqlDecimal` preserves decimal precision 29–38 end-to-end. Generated codecs use strict UTF-8 and include SQL type,
facets and enum underlying type in their version hash.

`[GenerateDto]` deliberately has no manual SQL facet/collation attributes. Use AutoContracts v3 for a production
contract that must preserve discovered SQL length, precision, scale and collation exactly; `SqlServerTvpColumnAttribute`
is only for manual TVP positional parameters.

## AutoContracts

`AutoContractsSourceGenerator` reads exactly one canonical v3 `caerius.contracts.json` through `AdditionalFiles` and one
optional override. The manifest contains server major/features, discovered procedures, parameters, TVPs and ordered
result metadata. It has zero-based continuous ordinals, exact SQL facets/collations and semantic hashes.

The override can replace result set 0 only when discovery returned `None`/`Undetermined`, then add unique continuous
sets 1–9. Its schema becomes part of the generated contract hash. SQL Server only describes the first result set
statically, so contract discovery never executes an application stored procedure to infer later result sets.

Generated contracts expose a typed `StoredProcedureCommandBuilder<TProcedure>` entry point. Parameters with `OUTPUT`,
`INPUTOUTPUT` and `RETURNVALUE` direction are represented in a generated output record. SQL catalog metadata cannot
distinguish output-only from input/output calls, so discovery emits `Output`; declare a parameter-direction override in
`caerius.contracts.overrides.json` when an explicit input value is required. The generator recomputes all hashes from
the metadata fields and ignores JSON `contractHash` values. Malformed manifests, malformed overrides and unknown
override procedures are source-generator errors. The generated API consumes the data reader before returning
`CaeriusCommandResult<TOutput>` or `CaeriusExecutionResult<TData, TOutput>`.

For result-bearing AutoContracts procedures, generated helpers expose materialized `QueryAsync` (`ImmutableArray<T>`),
`QueryEnumerableAsync` and `QueryReadOnlyCollectionAsync` variants for one and for 2–10 result sets.

## Multi-result API generator

The public package does not ship a multi-result source generator to applications. The build-only project in
`src/Compiler/CaeriusNet.MultiResultApiGenerator` is referenced only by the runtime and emits its overloads during the
runtime build.

It generates the three materialized families for every arity from **2 through 10**:

| API family                                        | Tuple elements                |
|---------------------------------------------------|-------------------------------|
| `QueryMultipleAsync<T1, …, Tn>`                   | materialized `IEnumerable<T>` |
| `QueryMultipleReadOnlyCollectionAsync<T1, …, Tn>` | `ReadOnlyCollection<T>`       |
| `QueryMultipleImmutableArrayAsync<T1, …, Tn>`     | `ImmutableArray<T>`           |

The same arities are generated for procedures with SQL output values. Every overload accepts one capacity per result set
and a `CaeriusResultSetCountPolicy`. The default policy requires exactly the announced sets; missing or extra sets are
contract errors.

## Architecture

```text
Attribute / AdditionalFiles input
        ↓
symbol-aware extraction into value-equatable models
        ↓
deterministic C# emission with stable hint names
```

| Area                                               | Responsibility                                                                  |
|----------------------------------------------------|---------------------------------------------------------------------------------|
| `Dto/`                                             | `[GenerateDto]` extraction and mapper/codec emission                            |
| `Tvp/`                                             | `[GenerateTvp]` extraction and static TVP contract emission                     |
| `AutoContracts/`                                   | v3 manifest parsing, override merging and typed contract emission               |
| `Helpers/` and `Models/`                           | shared type classification, syntax validation, names and value-equatable models |
| `src/Compiler/CaeriusNet.MultiResultApiGenerator/` | runtime-only multi-result overload generation                                   |

Generated sources are nullable-enabled, use stable hint names and contain no speculative `AggressiveInlining` or
`AggressiveOptimization` annotations.

## Build and test

```bash
dotnet build src/Compiler/CaeriusNet.Generator/CaeriusNet.Generator.csproj
dotnet test --project tests/CaeriusNet.Generator.Tests/CaeriusNet.Generator.Tests.csproj --report-trx --coverage
dotnet test --project tests/CaeriusNet.Analyzer.Tests/CaeriusNet.Analyzer.Tests.csproj --report-trx --coverage
```

Generator tests cover generated DTO mappers/codecs, static TVP writers, AutoContracts v3 manifests and incremental step
tracking. Analyzer tests cover the diagnostics and mirror the generator's symbol-level constructor rules.

## License

MIT
