# Table-Valued Parameters

Table-Valued Parameters (TVP) let you pass an entire set of rows — IDs, GUIDs, composite keys — as a single parameter to a SQL Server Stored Procedure. CaeriusNet implements TVP streaming via `IEnumerable<SqlDataRecord>`, which avoids `DataTable` allocation and streams data directly to the TDS protocol layer.

## What is a TVP?

A TVP is a SQL Server user-defined table type that can be passed as a read-only parameter (`READONLY`) to Stored Procedures. Instead of sending 1 000 IDs one by one, you send a single structured table with 1 000 rows in one round-trip.

```sql
-- 1. Create the SQL Server type
CREATE TYPE dbo.tvp_int AS TABLE (
    Id INT NOT NULL
);

-- 2. Use it in a Stored Procedure
CREATE PROCEDURE dbo.sp_GetUsers_By_Tvp_Ids
    @Ids dbo.tvp_int READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Username, Age
    FROM dbo.Users
    WHERE Id IN (SELECT Id FROM @Ids);
END
```

## C# implementation

### Source-generated (recommended)

Annotate a sealed partial record with `[GenerateTvp]`. Provide the SQL schema and type name:

```csharp
using CaeriusNet.Attributes.Tvp;

[GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
public sealed partial record UserIdTvp(int Id);
```

The generator emits `ITvpMapper<UserIdTvp>` with a zero-allocation `SqlDataRecord` streaming implementation. See [Source Generators](/documentation/source-generators) for details on the generated code.

### Manual implementation

Implement `ITvpMapper<T>` directly when you need full control:

```csharp
using CaeriusNet.Mappers;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;

public sealed record UserIdTvp(int Id) : ITvpMapper<UserIdTvp>
{
    public static string TvpTypeName => "dbo.tvp_int";

    public IEnumerable<SqlDataRecord> MapAsSqlDataRecords(IEnumerable<UserIdTvp> items)
    {
        var metaData = new[] { new SqlMetaData("Id", SqlDbType.Int) };
        var record = new SqlDataRecord(metaData);  // single instance reused across all rows
        foreach (var item in items)
        {
            record.SetInt32(0, item.Id);
            yield return record;    // Microsoft.Data.SqlClient reads values before advancing
        }
    }
}
```

::: tip Single-instance reuse
A single `SqlDataRecord` is created once and its values are overwritten before each `yield return`. `Microsoft.Data.SqlClient` reads all column values synchronously before moving to the next row, making this zero-copy pattern safe.
:::

## Using TVP with the builder

Pass the TVP collection via `AddTvpParameter`:

```csharp
var ids = users.Select(u => new UserIdTvp(u.Id)).ToList();

var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Tvp_Ids", 1024)
    .AddTvpParameter("Ids", ids)
    .Build();

var users = await dbContext.QueryAsIEnumerableAsync<UserDto>(sp, cancellationToken);
```

::: warning Empty collections throw
`AddTvpParameter` validates that the collection is non-empty. Passing an empty `IEnumerable<T>` throws `ArgumentException`. Validate before calling:

```csharp
if (ids.Count == 0) return [];
```
:::

## Combining TVP with regular parameters

Mix `.AddTvpParameter()` and `.AddParameter()` freely. Order does not matter for SQL Server, but match the SP parameter names exactly:

```sql
CREATE PROCEDURE dbo.sp_GetUsers_By_Tvp_Ids_And_Age
    @Ids   dbo.tvp_int READONLY,
    @Age   INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Username, Age
    FROM dbo.Users
    WHERE Id IN (SELECT Id FROM @Ids)
      AND Age >= @Age;
END
```

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Tvp_Ids_And_Age", 500)
    .AddTvpParameter("Ids", ids)
    .AddParameter("Age", minAge, SqlDbType.Int)
    .Build();
```

## Multi-column TVP

TVPs are not limited to a single column. Define as many columns as needed:

```sql
CREATE TYPE dbo.tvp_user_key AS TABLE (
    Id    INT  NOT NULL,
    Guid  UNIQUEIDENTIFIER NOT NULL
);
```

```csharp
[GenerateTvp(Schema = "dbo", TvpName = "tvp_user_key")]
public sealed partial record UserKeyTvp(int Id, Guid Guid);
```

## Performance notes

| Aspect | Detail |
|---|---|
| Allocation | Single `SqlDataRecord` allocated per call, reused across all rows |
| Protocol | TDS-native structured parameter — no XML serialization |
| Throughput | Scales to tens of thousands of rows efficiently |
| `DataTable` | Not used — avoids boxing all values into `object[]` rows |
| Empty guard | `ArgumentException` prevents empty TVP (SQL Server requires ≥ 1 row) |

---

**Next:** [Reading Data](/documentation/reading-data) — fetch result sets as `IEnumerable`, `ReadOnlyCollection`, or `ImmutableArray`.
