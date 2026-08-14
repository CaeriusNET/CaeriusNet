# Multi-result sets

A stored procedure can return more than one `SELECT` result. CaeriusNet maps each set to a separate DTO collection in a single round trip. You do not need extra queries or manual `NextResultAsync` calls.

This page demonstrates two scenarios: a dashboard read with three result sets, and a read with two result sets driven by a TVP filter.

## SQL Server objects

```sql
-- Dashboard summary with users, orders, and per-user statistics.
CREATE PROCEDURE Users.usp_Get_Dashboard
AS
BEGIN
    SET NOCOUNT ON;

    -- Set #1: users
    SELECT UserId, UserGuid
    FROM   Users.Users
    ORDER BY UserId;

    -- Set #2: orders
    SELECT OrderId, UserId, Label, Amount, CreatedAt
    FROM   Users.Orders
    ORDER BY OrderId;

    -- Set #3: per-user statistics
    SELECT  u.UserId,
            u.UserName,
            COUNT(o.OrderId)           AS OrdersCount,
            COALESCE(SUM(o.Amount), 0) AS TotalAmount
    FROM    Users.Users        AS u
    LEFT JOIN Users.Orders     AS o ON o.UserId = u.UserId
    GROUP BY u.UserId, u.UserName
    ORDER BY u.UserId;
END
GO

-- Users and their orders, filtered by a TVP of user IDs.
CREATE PROCEDURE Users.usp_Get_Users_With_Orders_By_Tvp
    @tvp Types.tvp_Int READONLY
AS
BEGIN
    SET NOCOUNT ON;

    -- Set #1: matching users
    SELECT u.UserId, u.UserGuid
    FROM   Users.Users AS u
    INNER JOIN @tvp    AS t ON t.UserId = u.UserId
    ORDER BY u.UserId;

    -- Set #2: their orders
    SELECT o.OrderId, o.UserId, o.Label, o.Amount, o.CreatedAt
    FROM   Users.Orders AS o
    INNER JOIN @tvp     AS t ON t.UserId = o.UserId
    ORDER BY o.OrderId;
END
GO
```

## DTO definitions

```csharp
using CaeriusNet;
using CaeriusNet.Commands;

[GenerateDto]
public sealed partial record UserDto(int UserId, Guid UserGuid);

[GenerateDto]
public sealed partial record OrderDto(
    int OrderId,
    int UserId,
    string Label,
    decimal Amount,
    DateTime CreatedAt);

[GenerateDto]
public sealed partial record UserStatsDto(
    int UserId,
    string UserName,
    int OrdersCount,
    decimal TotalAmount);
```

## 1. Read three result sets

A 3-tuple destructures the three sets directly at the call site. The DTO type at each position **must** match the columns of the corresponding `SELECT`.

```csharp
public async ValueTask<DashboardSnapshot> GetDashboardAsync(CancellationToken ct)
{
    var command = new StoredProcedureCommandBuilder(
            "Users", "usp_Get_Dashboard")
        .Build();

    var (users, orders, stats) = await DbContext
        .QueryMultipleReadOnlyCollectionAsync<UserDto, OrderDto, UserStatsDto>(
            command,
            capacity1: 25,
            capacity2: 25,
            capacity3: 25,
            cancellationToken: ct);

    return new DashboardSnapshot(users, orders, stats);
}
```

::: tip Telemetry tags
The Caerius client span records `db.system.name=microsoft.sql_server`, `db.operation.name=EXECUTE`, and the stored-procedure name. Result-set count and parameter values are not emitted as high-cardinality telemetry.
:::

## 2. Read two result sets with a TVP

You can combine a TVP input with a multi-result-set output in **one** round trip and **one** span:

```csharp
public async ValueTask<(IReadOnlyCollection<UserDto> Users, IReadOnlyCollection<OrderDto> Orders)>
    GetUsersWithOrdersByTvpAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken ct)
{
    if (userIds.Count == 0) return ([], []);

    IEnumerable<UsersIntTvp> tvp = userIds.Select(id => new UsersIntTvp(id));

    var command = new StoredProcedureCommandBuilder(
            "Users", "usp_Get_Users_With_Orders_By_Tvp")
        .AddTvpParameter("tvp", tvp)
        .Build();

    var (users, orders) = await DbContext
        .QueryMultipleReadOnlyCollectionAsync<UserDto, OrderDto>(
            command,
            capacity1: 25,
            capacity2: 25,
            cancellationToken: ct);

    return (users, orders);
}
```

::: tip Telemetry tags
TVP values are not exported in traces or metrics.
:::

## Available method families

| Method | Sets | Return type |
|---|---|---|
| `QueryMultipleReadOnlyCollectionAsync<T1, …, Tn>` | 2–10 | tuple of `ReadOnlyCollection<T>` |
| `QueryMultipleImmutableArrayAsync<T1, …, Tn>` | 2–10 | tuple of `ImmutableArray<T>` |
| `QueryMultipleAsync<T1, …, Tn>` | 2–10 | tuple of materialized `IEnumerable<T>` |

::: warning Result-set order is the contract
CaeriusNet maps result sets **positionally**. The first `SELECT` becomes `T1`, the second becomes `T2`, and so on. The DTO type passed at each position must match the columns of the corresponding `SELECT`. CaeriusNet does not match columns by name at runtime. By default it also rejects absent or extra result sets with `CaeriusContractException`; use `CaeriusResultSetCountPolicy` only for procedures intentionally designed to vary their output.
:::

---

**Next:** [Transactions](/examples/transactions) - commit, C#-side rollback, and SQL-side rollback.
