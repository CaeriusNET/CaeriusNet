# Table-Valued Parameters

A Table-Valued Parameter lets you pass an entire **set of rows** into a Stored Procedure as a single typed temporary table. This avoids dynamic SQL, IN-list size limits, and the overhead of multiple round-trips.

This page walks through three TVP shapes — single-int, single-Guid, composite `(int, Guid)` — and a TVP combined with a scalar write.

## SQL Server objects

```sql
-- 1. Schema for user-defined types
IF SCHEMA_ID(N'Types') IS NULL
    EXEC(N'CREATE SCHEMA Types AUTHORIZATION dbo;');
GO

-- 2. User-defined table types — one per column shape you need
CREATE TYPE Types.tvp_Int AS TABLE
(
    UserId INT NOT NULL
);
GO

CREATE TYPE Types.tvp_Guid AS TABLE
(
    UserGuid UNIQUEIDENTIFIER NOT NULL
);
GO

CREATE TYPE Types.tvp_IntGuid AS TABLE
(
    UserId   INT              NOT NULL,
    UserGuid UNIQUEIDENTIFIER NOT NULL
);
GO

-- 3. Stored procedures that consume each TVP shape
CREATE PROCEDURE Users.usp_Get_Users_From_TvpInt
    @tvp Types.tvp_Int READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.UserGuid
    FROM   Users.Users AS u
    INNER JOIN @tvp    AS t ON t.UserId = u.UserId
    ORDER BY u.UserId;
END
GO

CREATE PROCEDURE Users.usp_Get_Users_From_TvpGuid
    @tvp Types.tvp_Guid READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.UserGuid
    FROM   Users.Users AS u
    INNER JOIN @tvp    AS t ON t.UserGuid = u.UserGuid
    ORDER BY u.UserId;
END
GO

CREATE PROCEDURE Users.usp_Get_Users_From_TvpIntGuid
    @tvp Types.tvp_IntGuid READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.UserGuid
    FROM   Users.Users AS u
    INNER JOIN @tvp    AS t ON t.UserId = u.UserId AND t.UserGuid = u.UserGuid
    ORDER BY u.UserId;
END
GO
```

## C# TVP types — source-generated

CaeriusNet generates the `ITvpMapper<T>` implementation when you apply `[GenerateTvp]`. The constructor parameters must match the SQL UDT column order and types exactly:

```csharp
using CaeriusNet.Attributes.Tvp;

[GenerateTvp(Schema = "Types", TvpName = "tvp_Int")]
public sealed partial record UsersIntTvp(int UserId);

[GenerateTvp(Schema = "Types", TvpName = "tvp_Guid")]
public sealed partial record UsersGuidTvp(Guid UserGuid);

[GenerateTvp(Schema = "Types", TvpName = "tvp_IntGuid")]
public sealed partial record UsersIntGuidTvp(int UserId, Guid UserGuid);
```

## 1. TVP read — filter by integer IDs

```csharp
public async Task<IReadOnlyCollection<UserDto>> GetUsersByTvpIntAsync(
    CancellationToken ct)
{
    IEnumerable<UsersIntTvp> ids = [new(1), new(2), new(3), new(4)];

    var sp = new StoredProcedureParametersBuilder(
            "Users", "usp_Get_Users_From_TvpInt", ResultSetCapacity: 5)
        .AddTvpParameter("tvp", ids)   // matches @tvp in the SP (no leading @)
        .Build();

    return await DbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, ct);
}
```

::: tip Telemetry tags
`caerius.tvp.used = true` · `caerius.tvp.type_name = Types.tvp_Int`
:::

## 2. TVP read — filter by GUIDs

```csharp
public async Task<ImmutableArray<UserDto>> GetUsersByTvpGuidAsync(
    CancellationToken ct)
{
    IEnumerable<UsersGuidTvp> guids =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333")),
    ];

    var sp = new StoredProcedureParametersBuilder(
            "Users", "usp_Get_Users_From_TvpGuid", ResultSetCapacity: 5)
        .AddTvpParameter("tvp", guids)
        .Build();

    return await DbContext.QueryAsImmutableArrayAsync<UserDto>(sp, ct);
}
```

## 3. TVP read — composite `(int, Guid)` key filter

```csharp
public async Task<IEnumerable<UserDto>> GetUsersByTvpIntGuidAsync(
    CancellationToken ct)
{
    IEnumerable<UsersIntGuidTvp> pairs =
    [
        new(1, Guid.Parse("11111111-1111-1111-1111-111111111111")),
        new(2, Guid.Parse("22222222-2222-2222-2222-222222222222")),
    ];

    var sp = new StoredProcedureParametersBuilder(
            "Users", "usp_Get_Users_From_TvpIntGuid", ResultSetCapacity: 5)
        .AddTvpParameter("tvp", pairs)
        .Build();

    return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct);
}
```

## 4. TVP combined with a scalar write

You can mix TVP parameters with regular scalar parameters in the same call. Here the TVP carries the target user IDs and the scalars carry the order metadata:

```sql
CREATE PROCEDURE Users.usp_Create_Orders_For_Users
    @tvp    Types.tvp_Int READONLY,
    @Label  NVARCHAR(64),
    @Amount DECIMAL(10, 2)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO Users.Orders (UserId, Label, Amount)
    SELECT t.UserId, @Label, @Amount
    FROM   @tvp AS t;

    SELECT @@ROWCOUNT AS RowsInserted;
END
GO
```

```csharp
public async Task<int> CreateBatchOrdersAsync(
    IReadOnlyCollection<int> userIds,
    string label,
    decimal amount,
    CancellationToken ct)
{
    if (userIds.Count == 0) return 0;

    IEnumerable<UsersIntTvp> targetUserIds = userIds.Select(id => new UsersIntTvp(id));

    var sp = new StoredProcedureParametersBuilder("Users", "usp_Create_Orders_For_Users")
        .AddTvpParameter("tvp",     targetUserIds)
        .AddParameter   ("Label",  label,  SqlDbType.NVarChar)
        .AddParameter   ("Amount", amount, SqlDbType.Decimal)
        .Build();

    return await DbContext.ExecuteScalarAsync<int>(sp, ct) ?? 0;
}
```

## Notes & constraints

- The TVP parameter name in `AddTvpParameter("tvp", …)` must match the SP `@tvp` parameter name **without** the leading `@`.
- `READONLY` is **required** on TVP parameters in SQL Server Stored Procedures.
- TVP rows are streamed via `IEnumerable<SqlDataRecord>` directly into the TDS protocol — no `DataTable`, no boxing.
- `caerius.sp.parameters` always shows `[TVP]` for the TVP entry, even when `CaptureParameterValues = true`. The actual rows are never inlined into a span.
- `AddTvpParameter` throws `ArgumentException` for an empty collection — SQL Server rejects empty TVPs. Validate before calling.

---

**Next:** [Multi-Result Sets](/examples/multi-result-sets) — fetch multiple result sets in one round-trip.
