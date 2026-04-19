# DTO Mapping

CaeriusNet maps SQL Server result sets to C# DTOs at **compile time** — no reflection, no dynamic expression trees, no surprises at runtime.

## How it works

Every DTO must implement `ISpMapper<T>`, a static-interface contract that provides a single `MapFromDataReader` method. Reads are **ordinal-based**: columns are accessed by index rather than by name, which eliminates per-row string lookups and matches the TDS wire protocol directly.

```csharp
public static abstract T MapFromDataReader(SqlDataReader reader);
```

## The `ISpMapper<T>` interface

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

Column indices **must match** the `SELECT` column order in your Stored Procedure. Declare your DTO properties in the same order as the SP result columns.

## Column order contract

The column at position `0` in the result set is read by ordinal `0`. Your Stored Procedure defines the contract:

```sql
-- Columns: Id (0), Username (1), Age (2)
SELECT Id, Username, Age
FROM dbo.Users
WHERE Age >= @Age
```

The matching DTO:

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

Use `reader.IsDBNull(ordinal)` before reading nullable columns. For nullable reference types (`string?`) and nullable value types (`int?`):

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

## Special type conversions

Some C# types require explicit conversion from their SQL Server equivalents:

| C# type | SQL type | Conversion |
|---|---|---|
| `DateOnly` | `date` / `datetime2` | `DateOnly.FromDateTime(reader.GetDateTime(n))` |
| `TimeOnly` | `time` | `TimeOnly.FromDateTime(reader.GetDateTime(n))` |
| `byte[]` | `varbinary` | `(byte[])reader.GetValue(n)` |
| `ushort` | `int` | `(ushort)reader.GetInt32(n)` |

Example with `DateOnly` and `byte[]`:

```csharp
public sealed record DocumentDto(int Id, DateOnly CreatedDate, byte[] Content)
    : ISpMapper<DocumentDto>
{
    public static DocumentDto MapFromDataReader(SqlDataReader reader)
        => new(
            reader.GetInt32(0),
            DateOnly.FromDateTime(reader.GetDateTime(1)),
            (byte[])reader.GetValue(2));
}
```

## Enum mapping

Enums map to their underlying integer type in SQL. Cast the reader result:

```csharp
public enum UserStatus : byte { Active = 1, Inactive = 0 }

public sealed record UserDto(int Id, UserStatus Status)
    : ISpMapper<UserDto>
{
    public static UserDto MapFromDataReader(SqlDataReader reader)
        => new(reader.GetInt32(0), (UserStatus)reader.GetByte(1));
}
```

## Source generation (recommended)

Writing `MapFromDataReader` manually is straightforward but repetitive. Use `[GenerateDto]` to have the compiler emit it for you. See the [Source Generators](/documentation/source-generators) page for full details.

```csharp
using CaeriusNet.Attributes.Dto;

[GenerateDto]
public sealed partial record UserDto(int Id, string Username, byte Age);
```

## Common pitfalls

| Issue | Fix |
|---|---|
| `InvalidCastException` at runtime | Reader method doesn't match the SQL column type |
| `IndexOutOfRangeException` | Ordinal index doesn't correspond to actual column count |
| Null reference exception | Nullable column not guarded with `IsDBNull` |
| Wrong field values | SP column order doesn't match the DTO constructor order |

---

**Next:** [Source Generators](/documentation/source-generators) — eliminate the `MapFromDataReader` boilerplate entirely.
