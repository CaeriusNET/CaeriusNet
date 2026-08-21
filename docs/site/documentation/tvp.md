# Table-valued parameters

A v12 TVP is a static row contract. The builder creates a structured `SqlParameter` with the declared SQL type and writes all rows into one reused `SqlDataRecord`; it does not create a `DataTable` or inspect a first element.

```sql
CREATE TYPE dbo.OrderLineType AS TABLE
(
    ProductId int NOT NULL,
    Quantity int NOT NULL
);
```

Generate the mapper:

```csharp
[GenerateTvp(Schema = "dbo", TvpName = "OrderLineType")]
public sealed partial record OrderLine(int ProductId, int Quantity);
```

Or implement `ITvpMapper<T>` with static `SqlTypeName`, immutable `Metadata`, `CreateRecord` and `WriteRow` members.
For a manually declared positional record, place `SqlServerTvpColumnAttribute` on **every** parameter. It records the
SQL name and zero-based ordinal, `SqlDbType`, nullability, and all applicable length, precision, scale, LCID and
`SqlCompareOptions` facets; do not rely on inferred metadata.

```csharp
using System.Data;
using System.Data.SqlTypes;

[GenerateTvp(Schema = "dbo", TvpName = "CustomerKeyType")]
public sealed partial record CustomerKeyTvp(
    [SqlServerTvpColumn("CustomerId", 0, SqlDbType.Int, false)] int CustomerId,
    [SqlServerTvpColumn("Code", 1, SqlDbType.NVarChar, false,
        MaxLength = 32, LocaleId = 1033,
        CompareOptions = SqlCompareOptions.IgnoreCase | SqlCompareOptions.IgnoreKanaType)] string Code);
```

`SqlServerTvpColumnAttribute` is for manual TVP records only. DTOs deliberately do not infer or expose manual SQL
facets; use AutoContracts v3 when a production contract must preserve database facets/collation exactly.

```csharp
IReadOnlyCollection<OrderLine> lines = [new(42, 2)];

var command = new StoredProcedureCommandBuilder("dbo", "usp_Order_AddLines")
    .AddTvpParameter("Lines", lines)
    .Build();

await database.ExecuteAsync(command, ct);
```

The `IReadOnlyCollection<T>` overload is replayable and is the only TVP form permitted on a cacheable command. It
supports empty input, one row and large batches. Use the explicit factory overload for a non-replayable producer:

```csharp
.AddTvpParameter("Lines", static cancellationToken => ReadLines(cancellationToken))
```

The factory streams without materialization and is intentionally non-cacheable. Both forms are supported on SQL Server
2022/2025. The static contract is validated before a connection opens: structured parameters are input-only, require a
SQL type name and cannot have an empty metadata list.

The `SqlMetaData` column order and types must exactly match the user-defined table type.
