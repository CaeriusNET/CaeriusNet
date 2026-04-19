# Multiple Result Sets

SQL Server Stored Procedures can return more than one `SELECT` statement result. CaeriusNet exposes dedicated helpers that read up to five typed result sets from a single command execution — one round-trip, zero intermediate lists.

## When to use

Use multiple result sets when you need to fetch logically related data simultaneously:

- A dashboard query returning users + orders + summary totals
- A paginated response returning a data page + a total count
- A reporting query combining header + line items in one call

Compared to multiple separate stored procedure calls, a single multi-result SP reduces round-trips, connection pool pressure, and total latency.

## SQL Server setup

```sql
CREATE PROCEDURE dbo.sp_Get_Dashboard_Data
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: recent users
    SELECT Id, Username, Age
    FROM dbo.Users
    ORDER BY Id DESC;

    -- Result set 2: recent orders
    SELECT OrderId, UserId, Total
    FROM dbo.Orders
    ORDER BY OrderId DESC;
END
```

## Available overloads

CaeriusNet provides three collection-type families, each with overloads for 2 to 5 result sets:

### `QueryMultipleIEnumerableAsync`

Returns a tuple of `IEnumerable<T>` values:

```csharp
Task<(IEnumerable<T1>, IEnumerable<T2>)>
    QueryMultipleIEnumerableAsync<T1, T2>(context, sp, ct)

Task<(IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>)>
    QueryMultipleIEnumerableAsync<T1, T2, T3>(context, sp, ct)

// ... up to T5
```

### `QueryMultipleReadOnlyCollectionAsync`

Returns a tuple of `ReadOnlyCollection<T>` values:

```csharp
Task<(ReadOnlyCollection<T1>, ReadOnlyCollection<T2>)>
    QueryMultipleReadOnlyCollectionAsync<T1, T2>(context, sp, ct)
```

### `QueryMultipleImmutableArrayAsync`

Returns a tuple of `ImmutableArray<T>` values:

```csharp
Task<(ImmutableArray<T1>, ImmutableArray<T2>)>
    QueryMultipleImmutableArrayAsync<T1, T2>(context, sp, ct)
```

## Example: two result sets

```csharp
public sealed record UserRepository(ICaeriusNetDbContext DbContext)
    : IUserRepository
{
    public async Task<(IEnumerable<UserDto>, IEnumerable<OrderDto>)> GetDashboardAsync(
        CancellationToken cancellationToken)
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Get_Dashboard_Data", 128)
            .Build();

        return await DbContext.QueryMultipleIEnumerableAsync<UserDto, OrderDto>(
            sp, cancellationToken);
    }
}
```

Destructure the tuple at the call site:

```csharp
var (users, orders) = await repository.GetDashboardAsync(cancellationToken);
```

## Example: three result sets

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_Get_Report_Data", 500)
    .AddParameter("Month", month, SqlDbType.TinyInt)
    .Build();

var (users, orders, products) = await dbContext
    .QueryMultipleIEnumerableAsync<UserDto, OrderDto, ProductDto>(sp, cancellationToken);
```

## Result set order

The type parameters `T1`, `T2`, ... must match the **order** in which the SP returns its `SELECT` statements. The first type maps to the first result set, the second to the second, and so on.

::: warning Order is strict
If the SP changes its SELECT order, update the type parameters accordingly. There is no runtime name matching — sets are consumed sequentially from the `SqlDataReader`.
:::

## DTO requirements

Each type parameter must be a class implementing `ISpMapper<T>` (or generated with `[GenerateDto]`). The same ordinal-mapping rules apply as for single result sets.

---

**Next:** [Caching](/documentation/cache) — reduce database load with per-call Frozen, InMemory, or Redis caching.
