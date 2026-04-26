# DTO Mapping

CaeriusNet maps SQL Server result sets to C# DTOs at **compile time** — no reflection, no expression-tree compilation, no runtime metadata lookups. This page explains how the contract works, what it requires from your DTOs, and the few special cases worth knowing.

## How it works

Every DTO implements `ISpMapper<T>`, a static-interface contract with a single method:

```csharp
public static abstract T MapFromDataReader(SqlDataReader reader);
```

Reads are **ordinal-based**: each column is accessed by its zero-based index rather than by name. This eliminates per-row string lookups and matches the TDS wire protocol directly — the column order in your `SELECT` statement is the contract between SQL and C#.

You can implement `ISpMapper<T>` manually, or — preferably — let the source generator emit it for you with `[GenerateDto]`. Both produce the same machine code; the generator simply removes the boilerplate.

## A manual `ISpMapper<T>`

```csharp
using CaeriusNet.Mappers;
using Microsoft.Data.SqlClient;

public sealed record UserDto(int Id, string Username, byte Age)
    : ISpMapper<UserDto>
{
    public static UserDto MapFromDataReader(SqlDataReader reader)
        => new(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetByte(2));
}
```

The constructor parameter order **must** match the `SELECT` column order in your Stored Procedure.

## Column order is the contract

The column at position `0` in the result set is read at ordinal `0`. Your Stored Procedure defines the contract:

```sql
-- Columns: Id (0), Username (1), Age (2)
SELECT Id, Username, Age
FROM   dbo.Users
WHERE  Age >= @Age;
```

```csharp
public sealed record UserDto(int Id, string Username, byte Age)
    : ISpMapper<UserDto>
{
    public static UserDto MapFromDataReader(SqlDataReader reader)
        => new(reader.GetInt32(0), reader.GetString(1), reader.GetByte(2));
}
```

::: warning Column order matters
If the SP `SELECT` order changes, update the ordinal indices in `MapFromDataReader` accordingly. This is a deliberate design choice — ordinal reads are faster and enforce an explicit contract between SQL and C#.
:::

## Nullable columns

Use `reader.IsDBNull(ordinal)` before reading nullable columns. The pattern applies equally to nullable reference types (`string?`) and nullable value types (`int?`):

```csharp
public sealed record ItemDto(int Id, string? Description, int? Quantity)
    : ISpMapper<ItemDto>
{
    public static ItemDto MapFromDataReader(SqlDataReader reader)
        => new(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2));
}
```

The source generator emits these guards automatically when the constructor parameter is declared nullable.

## Special type conversions

A handful of C# types do not have a direct `Get*` method on `SqlDataReader` and require an explicit conversion:

| C# type | SQL type | Conversion expression |
|---|---|---|
| `DateOnly` | `date` / `datetime2` | `DateOnly.FromDateTime(reader.GetDateTime(n))` |
| `TimeOnly` | `time` | `TimeOnly.FromDateTime(reader.GetDateTime(n))` |
| `Half` | `real` | `(Half)reader.GetFloat(n)` |
| `byte[]` | `varbinary` / `image` | `reader.GetFieldValue<byte[]>(n)` |
| `ushort` | `int` | `(ushort)reader.GetInt32(n)` |

Example combining `DateOnly` and `byte[]`:

```csharp
public sealed record DocumentDto(int Id, DateOnly CreatedDate, byte[] Content)
    : ISpMapper<DocumentDto>
{
    public static DocumentDto MapFromDataReader(SqlDataReader reader)
        => new(
            reader.GetInt32(0),
            DateOnly.FromDateTime(reader.GetDateTime(1)),
            reader.GetFieldValue<byte[]>(2));
}
```

The source generator handles all of the above automatically.

## Enum mapping

Enums map to their underlying integer type in SQL. Cast the reader result accordingly:

```csharp
public enum UserStatus : byte { Inactive = 0, Active = 1 }

public sealed record UserDto(int Id, UserStatus Status)
    : ISpMapper<UserDto>
{
    public static UserDto MapFromDataReader(SqlDataReader reader)
        => new(reader.GetInt32(0), (UserStatus)reader.GetByte(1));
}
```

## Source generation (recommended)

Writing `MapFromDataReader` manually is straightforward but repetitive. Annotate your DTO with `[GenerateDto]` and the generator emits the implementation at build time:

```csharp
using CaeriusNet.Attributes.Dto;

[GenerateDto]
public sealed partial record UserDto(int Id, string Username, byte Age);
```

The DTO must be `sealed`, `partial`, and use a primary constructor — the [Roslyn analyzer](/documentation/diagnostics) reports a build error if any of these are missing. See the [Source Generators](/documentation/source-generators) page for the full list of features.

## Common pitfalls

| Symptom | Likely cause | Fix |
|---|---|---|
| `InvalidCastException` at runtime | Reader method does not match the SQL column type | Align the `Get*` call (or DTO field type) with the actual SQL type |
| `IndexOutOfRangeException` | Ordinal index does not correspond to an actual column | Re-check the SP `SELECT` arity |
| `NullReferenceException` on a column | Nullable column not guarded with `IsDBNull` | Make the field nullable or add the guard |
| Wrong values for several fields | SP column order does not match constructor parameter order | Re-align the SELECT with the constructor |

---

**Next:** [Source Generators](/documentation/source-generators) — eliminate the `MapFromDataReader` boilerplate entirely.
