# Multiple Result Sets

A SQL Server Stored Procedure can return more than one `SELECT` result. CaeriusNet exposes dedicated helpers that read up to **five typed result sets** from a single command execution — one round-trip, one connection, one telemetry span.

## When to use

Multiple result sets are the right tool whenever you need logically related data fetched together:

- A **dashboard** query returning users + orders + summary totals
- A **paginated** response returning a data page + a total count
- A **report** combining header + line items in one call

Compared to multiple separate SP calls, a single multi-result SP reduces round-trips, connection-pool pressure, and total latency — and it nests under a single span instead of fragmenting your trace.

## SQL Server setup

```sql
CREATE PROCEDURE dbo.sp_Get_Dashboard_Data
AS
BEGIN
    SET NOCOUNT ON;

    -- Result set 1: recent users
    SELECT Id, Username, Age
    FROM   dbo.Users
    ORDER BY Id DESC;

    -- Result set 2: recent orders
    SELECT OrderId, UserId, Total
    FROM   dbo.Orders
    ORDER BY OrderId DESC;
END
GO
```

## Available overloads

CaeriusNet offers three collection-type families, each with overloads for **2 to 5** result sets. Pick the family that matches your storage, mutation, and allocation needs:

### `QueryMultipleIEnumerableAsync`

Returns a tuple of `IEnumerable<T>`:

```csharp
Task<(IEnumerable<T1>, IEnumerable<T2>)>
    QueryMultipleIEnumerableAsync<T1, T2>(/* ... */);

Task<(IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>)>
    QueryMultipleIEnumerableAsync<T1, T2, T3>(/* ... */);

// ... up to T5
```

### `QueryMultipleReadOnlyCollectionAsync`

Returns a tuple of `ReadOnlyCollection<T>`:

```csharp
Task<(ReadOnlyCollection<T1>, ReadOnlyCollection<T2>)>
    QueryMultipleReadOnlyCollectionAsync<T1, T2>(/* ... */);
```

### `QueryMultipleImmutableArrayAsync`

Returns a tuple of `ImmutableArray<T>`:

```csharp
Task<(ImmutableArray<T1>, ImmutableArray<T2>)>
    QueryMultipleImmutableArrayAsync<T1, T2>(/* ... */);
```

## Example — two result sets

```csharp
public sealed record DashboardRepository(ICaeriusNetDbContext DbContext)
    : IDashboardRepository
{
    public async Task<(IEnumerable<UserDto> Users, IEnumerable<OrderDto> Orders)>
        GetDashboardAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Get_Dashboard_Data", 128)
            .Build();

        return await DbContext.QueryMultipleIEnumerableAsync<UserDto, OrderDto>(sp, ct);
    }
}
```

Destructure at the call site:

```csharp
var (users, orders) = await repository.GetDashboardAsync(ct);
```

## Example — three result sets

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_Get_Report_Data", 500)
    .AddParameter("Month", month, SqlDbType.TinyInt)
    .Build();

var (users, orders, products) = await DbContext
    .QueryMultipleIEnumerableAsync<UserDto, OrderDto, ProductDto>(sp, ct);
```

## Result-set order is the contract

The type parameters `T1`, `T2`, … must match the **order** in which the SP returns its `SELECT` statements. The first type maps to the first result set, the second to the second, and so on. There is no runtime name matching — sets are consumed sequentially from the `SqlDataReader`.

::: warning Order is strict
If the SP changes its `SELECT` order, update the type parameters accordingly. Misaligning them produces an `InvalidCastException` (best case) or silently wrong values (worst case) at runtime.
:::

## DTO requirements

Every type parameter must implement `ISpMapper<T>` (manual or generated with `[GenerateDto]`). The same ordinal-mapping rules described in [DTO Mapping](/documentation/dto-mapping) apply per result set.

## Telemetry

Multi-result-set calls produce a single span tagged with:

| Tag | Value |
|---|---|
| `caerius.resultset.multi` | `true` |
| `caerius.resultset.expected_count` | The number of result sets requested (2, 3, 4, or 5) |

The single span keeps the trace cohesive — there is no fan-out into one span per `SELECT`.

## Combining with TVPs and caching

Multi-result-set calls accept the same builder features as single-result calls — TVPs, scalar parameters, and any of the cache tiers. See [Advanced Usage](/documentation/advanced-usage#multiple-result-sets-with-tvp) for combined examples.

---

**Next:** [Caching](/documentation/cache) — reduce database load with per-call Frozen, InMemory, or Redis caching.
