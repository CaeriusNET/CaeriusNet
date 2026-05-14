# Table-valued parameters

Table-valued parameters (TVPs) let you pass an entire **set of rows** as a single typed parameter to a SQL Server stored procedure. Use them for IDs, GUIDs, composite keys, or wider row shapes when one stored procedure call should operate on many rows.

## Why TVPs?

A TVP is a SQL Server **user-defined table type** that you pass as a `READONLY` parameter. Instead of sending 1,000 IDs one by one or building dynamic SQL, you send one structured table in a single round trip.

```sql
-- 1. Create the SQL Server type
CREATE TYPE dbo.tvp_int AS TABLE
(
    Id INT NOT NULL
);
GO

-- 2. Use it in a stored procedure
CREATE PROCEDURE dbo.sp_GetUsers_By_Tvp_Ids
    @Ids dbo.tvp_int READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Username, Age
    FROM   dbo.Users
    WHERE  Id IN (SELECT Id FROM @Ids);
END
GO
```

## C# implementation

### Source-generated (recommended)

Annotate a sealed partial record with `[GenerateTvp]` and provide the SQL `Schema` and `TvpName`:

```csharp
using CaeriusNet.Attributes.Tvp;

[GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
public sealed partial record UserIdTvp(int Id);
```

The generator emits the `ITvpMapper<UserIdTvp>` implementation. See [source generators](/documentation/source-generators#generatetvp-tvp-mapper) for the supported type rules.

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
        var record   = new SqlDataRecord(metaData); // single instance reused across all rows

        foreach (var item in items)
        {
            record.SetInt32(0, item.Id);
            yield return record;
        }
    }
}
```

::: tip Why a single reused `SqlDataRecord`?
`Microsoft.Data.SqlClient` reads all column values synchronously before advancing to the next row, so overwriting the same instance between `yield return` calls is safe and avoids one allocation per row.
:::

## Using a TVP with the builder

Pass the TVP collection via `AddTvpParameter`:

```csharp
var ids = users.Select(u => new UserIdTvp(u.Id)).ToList();

var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Tvp_Ids", ResultSetCapacity: 1024)
    .AddTvpParameter("Ids", ids)
    .Build();

var matchedUsers = await dbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct);
```

::: warning Empty collections throw
`AddTvpParameter` requires a non-empty collection — SQL Server rejects empty TVPs. Validate before calling:

```csharp
if (ids.Count == 0) return [];
```
:::

## Combining a TVP with regular parameters

`AddTvpParameter` and `AddParameter` mix freely. Order does not matter for SQL Server, but parameter names must match the stored procedure definition exactly:

```sql
CREATE PROCEDURE dbo.sp_GetUsers_By_Tvp_Ids_And_Age
    @Ids dbo.tvp_int READONLY,
    @Age INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Username, Age
    FROM   dbo.Users
    WHERE  Id IN (SELECT Id FROM @Ids)
       AND Age >= @Age;
END
GO
```

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Tvp_Ids_And_Age", 500)
    .AddTvpParameter("Ids", ids)
    .AddParameter("Age", minAge, SqlDbType.Int)
    .Build();
```

## Multi-column TVPs

TVPs are not limited to a single column. Define as many columns as needed and the generator handles them all:

```sql
CREATE TYPE dbo.tvp_user_key AS TABLE
(
    Id   INT              NOT NULL,
    Guid UNIQUEIDENTIFIER NOT NULL
);
```

```csharp
[GenerateTvp(Schema = "dbo", TvpName = "tvp_user_key")]
public sealed partial record UserKeyTvp(int Id, Guid Guid);
```

## Usage guidance

| Recommendation | Reason |
|---|---|
| Validate input before calling | SQL Server rejects empty TVPs; return early when the input set is empty. |
| Keep TVP row types focused | Include only the columns the stored procedure actually needs. |
| Match SQL order exactly | `[GenerateTvp]` maps constructor parameters by position. |
| Prefer TVPs over dynamic `IN` lists | TVPs keep SQL parameterized and avoid query text growth. |
| Batch very large inputs | Large sets can increase command time and transaction duration. |

## Telemetry

When a TVP is attached, the corresponding stored procedure span is tagged accordingly:

| Tag | Value |
|---|---|
| `caerius.tvp.used` | `true` |
| `caerius.tvp.type_name` | The TVP type name (e.g. `dbo.tvp_int`); comma-separated when several TVPs are used |
| `caerius.sp.parameters` | `[TVP]` is shown for the TVP entry — the row data is never inlined into the span, even when `CaptureParameterValues = true` |

---

**Next:** [Reading data](/documentation/reading-data) - fetch result sets as `IEnumerable`, `ReadOnlyCollection`, or `ImmutableArray`.
