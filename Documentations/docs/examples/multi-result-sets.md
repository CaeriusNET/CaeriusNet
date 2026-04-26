# Multi-Result Sets

A Stored Procedure can return more than one `SELECT` result. CaeriusNet maps each set to a separate DTO collection in a single round-trip — no extra queries, no manual `NextResultAsync` calls, and a single cohesive span in the trace.

This page demonstrates two scenarios: a pure 3-set dashboard read, and a 2-set read driven by a TVP filter.

## SQL Server objects

```sql
-- 3-set: dashboard summary (users + orders + per-user stats)
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

-- 2-set: users + their orders, filtered by a TVP of user IDs
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

## 1. Three result sets — pure multi-RS

A 3-tuple destructures the three sets directly at the call site. The DTO type at each position **must** match the columns of the corresponding `SELECT` (positional contract):

```csharp
public async Task<DashboardSnapshot> GetDashboardAsync(CancellationToken ct)
{
    var sp = new StoredProcedureParametersBuilder(
            "Users", "usp_Get_Dashboard", capacity: 25)
        .Build();

    var (users, orders, stats) = await DbContext
        .QueryMultipleReadOnlyCollectionAsync<UserDto, OrderDto, UserStatsDto>(sp, ct);

    return new DashboardSnapshot(users, orders, stats);
}
```

::: tip Telemetry tags
`caerius.resultset.multi = true` · `caerius.resultset.expected_count = 3`
:::

## 2. Two result sets — TVP + multi-RS

You can combine a TVP input with a multi-result-set output — still **one** round-trip and **one** span:

```csharp
public async Task<(IReadOnlyCollection<UserDto> Users, IReadOnlyCollection<OrderDto> Orders)>
    GetUsersWithOrdersByTvpAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken ct)
{
    if (userIds.Count == 0) return ([], []);

    IEnumerable<UsersIntTvp> tvp = userIds.Select(id => new UsersIntTvp(id));

    var sp = new StoredProcedureParametersBuilder(
            "Users", "usp_Get_Users_With_Orders_By_Tvp", capacity: 25)
        .AddTvpParameter("tvp", tvp)
        .Build();

    var (users, orders) = await DbContext
        .QueryMultipleReadOnlyCollectionAsync<UserDto, OrderDto>(sp, ct);

    return (users, orders);
}
```

::: tip Telemetry tags
`caerius.tvp.used = true` · `caerius.resultset.multi = true` · `caerius.resultset.expected_count = 2`
:::

## Available overloads

| Method | Sets | Return type |
|---|---|---|
| `QueryMultipleReadOnlyCollectionAsync<T1, T2>` | 2 | `(ReadOnlyCollection<T1>, ReadOnlyCollection<T2>)` |
| `QueryMultipleReadOnlyCollectionAsync<T1, T2, T3>` | 3 | `(ReadOnlyCollection<T1>, …, ReadOnlyCollection<T3>)` |
| `QueryMultipleImmutableArrayAsync<T1, T2>` | 2 | `(ImmutableArray<T1>, ImmutableArray<T2>)` |
| `QueryMultipleImmutableArrayAsync<T1, T2, T3>` | 3 | `(ImmutableArray<T1>, …, ImmutableArray<T3>)` |
| `QueryMultipleIEnumerableAsync<T1, T2>` | 2 | `(IEnumerable<T1>, IEnumerable<T2>)` |
| `QueryMultipleIEnumerableAsync<T1, T2, T3>` | 3 | `(IEnumerable<T1>, …, IEnumerable<T3>)` |

The `IEnumerable`, `ReadOnlyCollection`, and `ImmutableArray` families are all available with arities **2 → 5**.

::: warning Result-set order is the contract
CaeriusNet maps result sets **positionally** — the first `SELECT` becomes `T1`, the second becomes `T2`, and so on. The DTO type passed at each position must match the columns of the corresponding `SELECT`. There is no runtime name-matching; misalignment produces an `InvalidCastException` (best case) or silently wrong values.
:::

---

**Next:** [Transactions](/examples/transactions) — commit, C#-side rollback, and SQL-side rollback.
