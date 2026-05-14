# Source generators

CaeriusNet includes source generators for DTO mappers and table-valued parameter mappers. They remove repetitive mapping code while keeping the mapping contract visible in your C# types.

You do not install a separate analyzer or generator package. The generators are included with the `CaeriusNet` NuGet package.

```bash
dotnet add package CaeriusNet
```

## When to use source generators

Use source generators when you want CaeriusNet to create mapper implementations from simple record or class definitions.

| Attribute | Use it for | Generated contract |
|---|---|---|
| `[GenerateDto]` | Stored procedure result rows | `ISpMapper<T>` |
| `[GenerateTvp]` | SQL Server table-valued parameter rows | `ITvpMapper<T>` |

The generator output is compile-time code. There is no runtime reflection or runtime mapper discovery.

## DTO mapper generation

Use `[GenerateDto]` on a sealed partial record or class with a primary constructor. Constructor parameters map to result-set columns by ordinal position.

```csharp
using CaeriusNet.Attributes.Dto;

[GenerateDto]
public sealed partial record UserDto(int Id, string Name, byte Age);
```

The stored procedure must return columns in the same order:

```sql
SELECT Id, Name, Age
FROM dbo.Users
ORDER BY Id;
```

### Requirements

- The type must be `sealed`.
- The type must be `partial`.
- The type must have a primary constructor.
- Constructor parameter order must match the stored procedure result-set order.

If a requirement is not met, the analyzer reports a build diagnostic. See [Diagnostic rules](/diagnostics/).

## Nullable columns

Use nullable C# types for columns that can return `NULL`.

```csharp
[GenerateDto]
public sealed partial record ProductDto(
    int Id,
    string Name,
    string? Description,
    int? Stock);
```

The generated mapper checks `IsDBNull` before reading nullable values.

## Supported common types

| C# type | Typical SQL Server type |
|---|---|
| `bool` | `bit` |
| `byte` | `tinyint` |
| `short` | `smallint` |
| `int` | `int` |
| `long` | `bigint` |
| `decimal` | `decimal` |
| `float` | `real` |
| `double` | `float` |
| `string` | `nvarchar` |
| `DateTime` | `datetime2` |
| `DateOnly` | `date` |
| `TimeOnly` | `time` |
| `DateTimeOffset` | `datetimeoffset` |
| `TimeSpan` | `time` |
| `Guid` | `uniqueidentifier` |
| `byte[]` | `varbinary` |
| Enums | The enum underlying type |

For unsupported types, the analyzer reports a diagnostic so you can choose a safer DTO shape.

## TVP mapper generation

Use `[GenerateTvp]` on a sealed partial record or class. The constructor parameters define the TVP row shape.

```csharp
using CaeriusNet.Attributes.Tvp;

[GenerateTvp(Schema = "dbo", TvpName = "tvp_UserId")]
public sealed partial record UserIdTvp(int Id);
```

The SQL type must match the C# constructor order and compatible SQL types:

```sql
CREATE TYPE dbo.tvp_UserId AS TABLE
(
    Id INT NOT NULL
);
```

Use the generated TVP with `AddTvpParameter`.

```csharp
var ids = userIds.Select(id => new UserIdTvp(id));

var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Ids", 256)
    .AddTvpParameter("Ids", ids)
    .Build();
```

### TVP requirements

- `Schema` must match the SQL Server type schema.
- `TvpName` must match the SQL Server type name.
- Constructor parameter order must match the SQL type column order.
- Nullable C# parameters should match nullable SQL columns.
- The input collection must contain at least one row.

## Manual mapper comparison

You can still write mappers manually when you need full control.

| Task | Generated mapper | Manual mapper |
|---|---|---|
| DTO row mapping | Add `[GenerateDto]` to a DTO | Implement `ISpMapper<T>` |
| TVP row mapping | Add `[GenerateTvp]` to a TVP row type | Implement `ITvpMapper<T>` |
| Contract validation | Analyzer diagnostics at build time | Your tests and runtime behavior |
| Boilerplate | Minimal | You write every reader or TVP call |

## Inspect generated files

If you need to troubleshoot generated output, enable compiler-generated files in your project.

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>obj\Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

You usually do not need this setting. It is useful when you are diagnosing a mapper issue or reviewing what code is compiled.

## Related content

- [DTO mapping](/documentation/dto-mapping)
- [Table-valued parameters](/documentation/tvp)
- [Compiler diagnostics](/documentation/diagnostics)
- [Diagnostic rules](/diagnostics/)
