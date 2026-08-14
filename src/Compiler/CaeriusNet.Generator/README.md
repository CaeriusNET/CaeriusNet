# CaeriusNet source generators

CaeriusNet v12 uses incremental Roslyn generators to produce compile-time SQL mapping code for DTOs, TVPs and
AutoContracts. The public generator targets `netstandard2.0` so it can run inside the .NET 10 compiler, while its Roslyn
dependency is pinned to `Microsoft.CodeAnalysis.CSharp` 5.0.0. Consumers need .NET 10 and compile with C# 14.

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

Constructor selection is symbol-based and deterministic. DTO and TVP types must satisfy the analyzer's structural rules
(including `sealed`, `partial` and a supported primary-constructor shape).

## AutoContracts

`AutoContractsSourceGenerator` reads `caerius.contracts.json` through `AdditionalFiles`. The v2 manifest contains
discovered server, procedure, parameter, TVP and first-result-set metadata. For result sets two through ten, add the
explicit `caerius.contracts.overrides.json` file.

Override result-set indices are unique, continuous and in the range 0–9. Their schema becomes part of the generated
contract hash. SQL Server only describes the first result set statically, so contract discovery never executes an
application stored procedure to infer later result sets.

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
| `AutoContracts/`                                   | v2 manifest parsing, override merging and typed contract emission               |
| `Helpers/` and `Models/`                           | shared type classification, syntax validation, names and value-equatable models |
| `src/Compiler/CaeriusNet.MultiResultApiGenerator/` | runtime-only multi-result overload generation                                   |

Generated sources are nullable-enabled, use stable hint names and contain no speculative `AggressiveInlining` or
`AggressiveOptimization` annotations.

## Build and test

```bash
dotnet build src/Compiler/CaeriusNet.Generator/CaeriusNet.Generator.csproj
dotnet test tests/CaeriusNet.Generator.Tests/CaeriusNet.Generator.Tests.csproj
dotnet test tests/CaeriusNet.Analyzer.Tests/CaeriusNet.Analyzer.Tests.csproj
```

Generator tests cover generated DTO mappers/codecs, static TVP writers, AutoContracts v2 manifests and incremental step
tracking. Analyzer tests cover the diagnostics and mirror the generator's symbol-level constructor rules.

## License

MIT
