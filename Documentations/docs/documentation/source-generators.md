# Source Generators

CaeriusNet ships two Roslyn incremental source generators that eliminate mapping boilerplate at compile time. Both generators run as part of your build — they produce zero runtime overhead and are fully AOT-compatible.

## Overview

| Generator | Attribute | Interface generated |
|---|---|---|
| `DtoSourceGenerator` | `[GenerateDto]` | `ISpMapper<T>` |
| `TvpSourceGenerator` | `[GenerateTvp]` | `ITvpMapper<T>` |

Both generators target **sealed partial records or classes**. The `partial` keyword lets the generator add the interface implementation as a second partial declaration alongside your type.

## `[GenerateDto]` — DTO mapper

Annotate a sealed partial record or class with `[GenerateDto]`. The generator emits a `MapFromDataReader` method with ordinal-based column reads, correct nullability guards, and special type conversions.

### Requirements

- Type must be `sealed`
- Type must be `partial`
- Type must use a primary constructor (the constructor parameters become the mapped columns, in order)

### Basic example

```csharp
using CaeriusNet.Attributes.Dto;

[GenerateDto]
public sealed partial record UserDto(int Id, string Username, byte Age);
```

**Generated code** (simplified):

```csharp
// UserDto.g.cs — auto-generated, do not edit
partial record UserDto : ISpMapper<UserDto>
{
    public static UserDto MapFromDataReader(SqlDataReader reader)
        => new(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetByte(2));
}
```

### Nullable fields

The generator respects C# nullable annotations and inserts `IsDBNull` guards automatically:

```csharp
[GenerateDto]
public sealed partial record ProductDto(int Id, string? Description, int? Stock);
```

Generated:

```csharp
public static ProductDto MapFromDataReader(SqlDataReader reader)
    => new(
        reader.GetInt32(0),
        reader.IsDBNull(1) ? null : reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetInt32(2));
```

### Special types

| Field type | Generated expression |
|---|---|
| `DateOnly` | `DateOnly.FromDateTime(reader.GetDateTime(n))` |
| `TimeOnly` | `TimeOnly.FromDateTime(reader.GetDateTime(n))` |
| `byte[]` | `(byte[])reader.GetValue(n)` |

## `[GenerateTvp]` — TVP mapper

Annotate a sealed partial record or class with `[GenerateTvp]`. The generator emits:
- `TvpTypeName` static property (e.g., `"dbo.tvp_int"`)
- `_tvpMetaData` static `SqlMetaData[]` field (one entry per property)
- `MapAsSqlDataRecords` iterator that **reuses a single `SqlDataRecord`** instance across all rows (zero-copy streaming)

### Requirements

- Type must be `sealed`
- Type must be `partial`
- Attribute requires `Schema` and `TvpName` named arguments

### Basic example

```csharp
using CaeriusNet.Attributes.Tvp;

[GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
public sealed partial record UserIdTvp(int Id);
```

**Generated code** (simplified):

```csharp
// UserIdTvp.g.cs — auto-generated, do not edit
partial record UserIdTvp : ITvpMapper<UserIdTvp>
{
    public static string TvpTypeName => "dbo.tvp_int";

    private static readonly SqlMetaData[] _tvpMetaData =
    [
        new SqlMetaData("Id", SqlDbType.Int)
    ];

    public IEnumerable<SqlDataRecord> MapAsSqlDataRecords(IEnumerable<UserIdTvp> items)
    {
        var record = new SqlDataRecord(_tvpMetaData);
        foreach (var item in items)
        {
            record.SetInt32(0, item.Id);
            yield return record;
        }
    }
}
```

### Nullable TVP fields

The generator emits `record.SetDBNull(n)` for nullable fields when the value is null:

```csharp
[GenerateTvp(Schema = "dbo", TvpName = "tvp_optional")]
public sealed partial record OptionalTvp(int Id, int? OptionalValue);
```

### Custom schema

Use a schema other than `dbo` to match your SQL Server type:

```csharp
[GenerateTvp(Schema = "Types", TvpName = "tvp_UserId")]
public sealed partial record UserIdTvp(int Id);
// TvpTypeName => "Types.tvp_UserId"
```

## Manual vs. generated comparison

| Aspect | Manual | Generated |
|---|---|---|
| `MapFromDataReader` | You write it | Compiler emits it |
| Ordinal indices | You manage them | Auto-assigned from constructor order |
| Nullable guards | You add `IsDBNull` | Emitted when field is nullable |
| `SqlDataRecord` reuse | You implement it | Always reused (single instance) |
| Compilation | Compiles as-is | Requires `sealed partial` |
| Build-time errors | Runtime | Compile-time |

## Enabling the generators

The generators ship **inside the CaeriusNet NuGet package** as an embedded Roslyn analyzer. No additional package or MSBuild configuration is required.

```shell
dotnet add package CaeriusNet
```

Once added, `[GenerateDto]` and `[GenerateTvp]` are available in the `CaeriusNet.Attributes.Dto` and `CaeriusNet.Attributes.Tvp` namespaces.

::: tip Inspecting generated code
Set `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` in your `.csproj` to write generated files to `obj/Generated/` for inspection.
:::

---

**Next:** [Table-Valued Parameters](/documentation/tvp) — bulk inputs without DataTable overhead.
