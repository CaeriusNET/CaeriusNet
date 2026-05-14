# Multiple result sets

Use a multiple-result-set query when one stored procedure returns related data in separate `SELECT` statements. CaeriusNet reads those result sets in order and returns a typed tuple.

Multiple result sets are useful for dashboards, detail pages, reports, and API responses that need related data from one database round trip.

## When to use multiple result sets

Use multiple result sets when:

- The data is requested together by the same use case.
- The stored procedure can return each shape with a clear `SELECT`.
- You want one database round trip instead of several calls.
- The result-set order is stable and versioned with the stored procedure contract.

Do not use multiple result sets when each data set has a different cache lifetime or authorization rule. In those cases, separate calls can be clearer.

## SQL Server example

```sql
CREATE PROCEDURE dbo.sp_Get_Dashboard_Data
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Age
    FROM dbo.Users
    ORDER BY Id;

    SELECT OrderId, UserId, Total
    FROM dbo.Orders
    ORDER BY OrderId;
END
GO
```

The first `SELECT` maps to the first generic type argument. The second `SELECT` maps to the second generic type argument.

## Call the stored procedure

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_Get_Dashboard_Data", 128)
    .Build();

var (users, orders) = await dbContext
    .QueryMultipleReadOnlyCollectionAsync<UserDto, OrderDto>(sp, ct);
```

## Available method families

Each method family supports two to five result sets.

| Method family | Returns |
|---|---|
| `QueryMultipleIEnumerableAsync<T1, T2>(...)` through `QueryMultipleIEnumerableAsync<T1, T2, T3, T4, T5>(...)` | Tuple of `IEnumerable<T>` values |
| `QueryMultipleReadOnlyCollectionAsync<T1, T2>(...)` through `QueryMultipleReadOnlyCollectionAsync<T1, T2, T3, T4, T5>(...)` | Tuple of `ReadOnlyCollection<T>` values |
| `QueryMultipleImmutableArrayAsync<T1, T2>(...)` through `QueryMultipleImmutableArrayAsync<T1, T2, T3, T4, T5>(...)` | Tuple of `ImmutableArray<T>` values |

## Choose a return shape

| Shape | Use when |
|---|---|
| `IEnumerable<T>` | You want a simple materialized sequence for LINQ operations. |
| `ReadOnlyCollection<T>` | You expose the result through an API and want an indexable read-only contract. |
| `ImmutableArray<T>` | You want a compact immutable value that is efficient to pass around. |

## Result-set order is the contract

The type parameters must match the SQL `SELECT` order.

```csharp
var (users, orders, stats) = await dbContext
    .QueryMultipleImmutableArrayAsync<UserDto, OrderDto, UserStatsDto>(sp, ct);
```

In this example:

1. The first result set maps to `UserDto`.
2. The second result set maps to `OrderDto`.
3. The third result set maps to `UserStatsDto`.

CaeriusNet does not match result sets by name. It consumes the `SqlDataReader` sequentially.

::: warning Keep the SQL contract stable
If you reorder, add, or remove `SELECT` statements in the stored procedure, update the C# call site and DTOs at the same time.
:::

## Missing trailing result sets

If the stored procedure returns fewer result sets than the method expects, CaeriusNet returns empty collections for the missing trailing sets.

```csharp
var (users, counts, optionalDetails) = await dbContext
    .QueryMultipleImmutableArrayAsync<UserDto, CountDto, DetailDto>(sp, ct);

if (optionalDetails.IsEmpty)
{
    // The procedure did not return the optional trailing detail set.
}
```

## Combine with TVPs

You can combine TVP inputs with multiple result-set outputs.

```csharp
var ids = userIds.Select(id => new UserIdTvp(id));

var sp = new StoredProcedureParametersBuilder("dbo", "sp_Get_Users_And_Orders_By_Ids", 256)
    .AddTvpParameter("Ids", ids)
    .Build();

var (users, orders) = await dbContext
    .QueryMultipleReadOnlyCollectionAsync<UserDto, OrderDto>(sp, ct);
```

## Caching behavior

`QueryMultiple*Async` methods do not apply `StoredProcedureParametersBuilder` cache policies. If you need to cache a multi-result response, cache the complete repository-level tuple in your application layer.

## Telemetry

Multiple result-set calls emit a single stored procedure span. The span includes the expected result-set count.

| Tag | Value |
|---|---|
| `caerius.resultset.multi` | `true` |
| `caerius.resultset.expected_count` | `2`, `3`, `4`, or `5` |

## Related content

- [Reading data](/documentation/reading-data)
- [Table-valued parameters](/documentation/tvp)
- [Multi-result set examples](/examples/multi-result-sets)
- [API reference](/documentation/api#multiple-result-sets)
