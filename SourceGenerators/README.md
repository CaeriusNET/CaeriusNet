# CaeriusNet Source Generators

Roslyn incremental source generators that emit compile-time mappers for DTOs and TVPs. Targets `netstandard2.0` (Roslyn
analyzer constraint). Uses `Microsoft.CodeAnalysis.CSharp 5.3.0`.

## Architecture

### Pipeline

```
ForAttributeWithMetadataName → Extract (value-equatable models) → Emit (pure string transforms)
```

Each generator follows the same three-stage pipeline:

1. **Filter** — `ForAttributeWithMetadataName` selects types annotated with `[GenerateDto]` or `[GenerateTvp]`.
2. **Extract** — Transforms syntax/semantic model into a value-equatable model (`DtoModel` / `TvpModel`). No Roslyn
   types flow past this stage.
3. **Emit** — Pure function that converts the model into generated C# source via `StringBuilder`.

Generated files use stable, generator-specific hint names so outputs remain distinct when type names repeat across
namespaces or generator kinds.

Generated files use stable, generator-specific hint names so outputs remain distinct when type names repeat across
namespaces or generator kinds.

### Key Design Decisions

| Decision                                    | Rationale                                                           |
|---------------------------------------------|---------------------------------------------------------------------|
| Value-equatable models                      | Enables Roslyn incremental caching — unchanged inputs skip emission |
| `EquatableArray<T>` wrapper                 | Provides element-by-element equality for `ImmutableArray<T>`        |
| `StringBuilder` with pre-allocated capacity | Minimizes allocations during code emission                          |
| `netstandard2.0` target                     | Required by Roslyn analyzer hosting — runs in any .NET SDK          |

## Project Structure

| File / Directory                          | Purpose                                                             |
|-------------------------------------------|---------------------------------------------------------------------|
| `Dto/DtoSourceGenerator.cs`               | `IIncrementalGenerator` for `[GenerateDto]` — emits `ISpMapper<T>`  |
| `Dto/DtoExtractor.cs`                     | Extracts `DtoModel` from syntax/semantic model                      |
| `Dto/DtoEmitter.cs`                       | Emits `MapFromDataReader` source code                               |
| `Tvp/TvpSourceGenerator.cs`               | `IIncrementalGenerator` for `[GenerateTvp]` — emits `ITvpMapper<T>` |
| `Tvp/TvpExtractor.cs`                     | Extracts `TvpModel` from syntax/semantic model                      |
| `Tvp/TvpEmitter.cs`                       | Emits `MapAsSqlDataRecords` + `TvpTypeName` source code             |
| `Helpers/TypeDetector.cs`                 | C# type → SQL type mapping and reader method resolution             |
| `Helpers/ColumnExtractor.cs`              | Shared column extraction from primary constructor parameters        |
| `Helpers/HintNameBuilder.cs`              | Stable unique hint names for generated source files                 |
| `Helpers/NamespaceHelper.cs`              | Namespace resolution for generated files                            |
| `Helpers/TypeStructureValidator.cs`       | Validates sealed, partial, primary constructor constraints          |
| `Helpers/SqlMetaDataExpressionBuilder.cs` | Builds `SqlMetaData` constructor expressions for TVPs               |
| `Models/DtoModel.cs`                      | Value-equatable DTO pipeline model                                  |
| `Models/TvpModel.cs`                      | Value-equatable TVP pipeline model                                  |
| `Models/ColumnModel.cs`                   | Describes a single primary constructor parameter                    |
| `Models/ColumnKind.cs`                    | Enum classifying parameter mapping behavior                         |
| `Models/EquatableArray.cs`                | `ImmutableArray<T>` wrapper with element equality                   |

## Supported Type Mappings

| C# Type          | SQL Server Type    | Reader Method           | SqlMetaData                  |
|------------------|--------------------|-------------------------|------------------------------|
| `bool`           | `bit`              | `GetBoolean`            | `SqlDbType.Bit`              |
| `byte`           | `tinyint`          | `GetByte`               | `SqlDbType.TinyInt`          |
| `short`          | `smallint`         | `GetInt16`              | `SqlDbType.SmallInt`         |
| `int`            | `int`              | `GetInt32`              | `SqlDbType.Int`              |
| `long`           | `bigint`           | `GetInt64`              | `SqlDbType.BigInt`           |
| `decimal`        | `decimal`          | `GetDecimal`            | `SqlDbType.Decimal`          |
| `float`          | `real`             | `GetFloat`              | `SqlDbType.Real`             |
| `Half`           | `real`             | `GetFloat` (cast)       | `SqlDbType.Real`             |
| `double`         | `float`            | `GetDouble`             | `SqlDbType.Float`            |
| `string`         | `nvarchar`         | `GetString`             | `SqlDbType.NVarChar`         |
| `char`           | `nchar`            | `GetString` (cast)      | `SqlDbType.NChar`            |
| `DateTime`       | `datetime2`        | `GetDateTime`           | `SqlDbType.DateTime2`        |
| `DateOnly`       | `date`             | `DateOnly.FromDateTime` | `SqlDbType.Date`             |
| `TimeOnly`       | `time`             | `TimeOnly.FromTimeSpan` | `SqlDbType.Time`             |
| `DateTimeOffset` | `datetimeoffset`   | `GetDateTimeOffset`     | `SqlDbType.DateTimeOffset`   |
| `TimeSpan`       | `time`             | `GetTimeSpan`           | `SqlDbType.Time`             |
| `Guid`           | `uniqueidentifier` | `GetGuid`               | `SqlDbType.UniqueIdentifier` |
| `byte[]`         | `varbinary`        | `GetFieldValue<byte[]>` | `SqlDbType.VarBinary`        |
| Enums            | (underlying type)  | (underlying reader)     | (underlying SqlDbType)       |

Types without a native mapping still generate code but fall back to `sql_variant`. The companion analyzer reports
CAERIUS005 on those parameters.

## Diagnostics

| ID         | Severity | Description                                    |
|------------|----------|------------------------------------------------|
| CAERIUS001 | Error    | Type must be `sealed`                          |
| CAERIUS002 | Error    | Type must be `partial`                         |
| CAERIUS003 | Error    | Must have primary constructor with parameters  |
| CAERIUS004 | Error    | `[GenerateTvp]` requires non-empty `TvpName`   |
| CAERIUS005 | Warning  | No native SQL mapping → `sql_variant` fallback |

User-facing diagnostics are emitted by `CaeriusNet.Analyzer`, not by the incremental generators themselves. All
diagnostics use category `CaeriusNet.Generator` and link to
`https://github.com/CaeriusNET/CaeriusNet/blob/main/Documentations/diagnostics/`.

## Generated Code Features

- `[GeneratedCode("CaeriusNet.Generator", "10.3.0")]` attribute on generated types
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on `MapFromDataReader`
- `[MethodImpl(MethodImplOptions.AggressiveOptimization)]` on `MapAsSqlDataRecords`
- `#pragma warning disable CS1591` (suppresses missing XML docs warning)
- `#nullable enable`
- XML doc comments on generated methods
- Ordinal constants for DTOs with >8 columns
- Single reused `SqlDataRecord` + schema array per TVP type (zero per-row allocation)

## Building

```bash
dotnet build SourceGenerators/CaeriusNet.Generator.csproj
```

## Testing

```bash
dotnet test Tests/CaeriusNet.Generator.Tests
```

Generator tests cover DTO generation, TVP generation, silent skip behavior, type detection, and edge cases. Analyzer
diagnostics are covered in `CaeriusNet.Analyzer.Tests`.

## Prerequisites

- .NET SDK (any version that supports `netstandard2.0`)
- No runtime dependencies — analyzer-only package

## License

MIT
