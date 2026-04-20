# CAERIUS006 — Unsupported type for SQL Server mapping

**Severity**: Warning
**Category**: CaeriusNet.Generator

## Cause

A property on a `[GenerateDto]` or `[GenerateTvp]` type uses a CLR type that has no native
SQL Server equivalent. The generated code falls back to `SqlDbType.Variant` (`sql_variant`).

Common offenders: `System.Int128`, `System.UInt128`, `System.Numerics.BigInteger`,
`System.Text.Rune`, `System.Range`, custom structs without a known SQL mapping.

## Why it matters

`sql_variant` disables critical SQL Server features:

- **No indexing** — columns cannot participate in indexes (no seek/scan optimizations).
- **No computed columns** — cannot be referenced in computed column definitions.
- **No type safety** — SQL Server cannot validate or constrain the stored value.
- **Performance penalties** — boxing/unboxing on every read and write.
- **Limited tooling** — many ORM and reporting tools handle `sql_variant` poorly.

## How to fix

Replace the unsupported type with a natively mapped CLR type:

```csharp
// ❌ Before — emits sql_variant, triggers CAERIUS006
[GenerateDto]
public sealed partial record LargeIdDto(int Id, Int128 LargeValue);

// ✅ After — use decimal (maps to SQL decimal/numeric)
[GenerateDto]
public sealed partial record LargeIdDto(int Id, decimal LargeValue);
```

```csharp
// ❌ Before — UInt128 has no SQL equivalent
[GenerateTvp(Schema = "dbo", TvpName = "tvp_hash")]
public sealed partial record HashTvp(UInt128 HashValue);

// ✅ After — store as byte[] (maps to SQL varbinary)
[GenerateTvp(Schema = "dbo", TvpName = "tvp_hash")]
public sealed partial record HashTvp(byte[] HashValue);
```

Common conversions:

| Unsupported type | Recommended replacement | SQL Server type |
|---|---|---|
| `Int128` | `decimal` | `decimal(38, 0)` |
| `UInt128` | `byte[]` (16 bytes) | `varbinary(16)` |
| `BigInteger` | `decimal` or `byte[]` | `decimal` / `varbinary(max)` |
| `Rune` | `char` or `string` | `nchar(1)` / `nvarchar` |
| `Range` | Two separate `int` properties | `int` + `int` |

## Supported types reference

The following CLR types are natively supported by CaeriusNet's source generators:

| C# type | SQL Server type | Reader method |
|---|---|---|
| `bool` | `bit` | `GetBoolean` |
| `byte` | `tinyint` | `GetByte` |
| `short` | `smallint` | `GetInt16` |
| `int` | `int` | `GetInt32` |
| `long` | `bigint` | `GetInt64` |
| `float` | `real` | `GetFloat` |
| `double` | `float` | `GetDouble` |
| `decimal` | `decimal` | `GetDecimal` |
| `Half` | `real` | `GetFloat` (cast) |
| `string` | `nvarchar` | `GetString` |
| `char` | `nchar(1)` | `GetString` (index) |
| `DateTime` | `datetime2` | `GetDateTime` |
| `DateOnly` | `date` | `GetDateTime` → `DateOnly.FromDateTime` |
| `TimeOnly` | `time` | `GetDateTime` → `TimeOnly.FromDateTime` |
| `DateTimeOffset` | `datetimeoffset` | `GetDateTimeOffset` |
| `TimeSpan` | `time` | `GetTimeSpan` |
| `Guid` | `uniqueidentifier` | `GetGuid` |
| `byte[]` | `varbinary` | `GetFieldValue<byte[]>` |

## Suppressing the warning

If `sql_variant` is intentional for your use case, suppress the diagnostic:

```csharp
#pragma warning disable CAERIUS006
[GenerateDto]
public sealed partial record FlexibleDto(int Id, Int128 FlexValue);
#pragma warning restore CAERIUS006
```

Or via `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.CAERIUS006.severity = none
```
