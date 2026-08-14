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

```csharp
var command = new StoredProcedureCommandBuilder("dbo", "usp_Order_AddLines")
    .AddTvpParameter("Lines", lines)
    .Build();

await database.ExecuteAsync(command, ct);
```

Empty input, one row, large batches and lazily produced sequences are supported on SQL Server 2022/2025. The static contract is validated before a connection opens: structured parameters are input-only, require a SQL type name and cannot have an empty metadata list.

The `SqlMetaData` column order and types must exactly match the user-defined table type.
