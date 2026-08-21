# Multiple result sets

Use one stored procedure when related data belongs to the same request and should be read in one database round trip. CaeriusNet provides generated overloads for **2 through 10** result sets and three materialized tuple families.

```sql
CREATE PROCEDURE dbo.usp_Dashboard_Get
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name FROM dbo.Users ORDER BY Id;
    SELECT Id, UserId, Total FROM dbo.Orders ORDER BY Id;
END
```

```csharp
var command = new StoredProcedureCommandBuilder("dbo", "usp_Dashboard_Get")
    .WithResultSetCapacities(128, 512)
    .Build();

var (users, orders) = await database
    .QueryMultipleReadOnlyCollectionAsync<User, Order>(command, ct);
```

## Collection families

| API family | Tuple elements |
| --- | --- |
| `QueryMultipleAsync<T1, …>` | `IEnumerable<T>` (materialized) |
| `QueryMultipleReadOnlyCollectionAsync<T1, …>` | `ReadOnlyCollection<T>` |
| `QueryMultipleImmutableArrayAsync<T1, …>` | `ImmutableArray<T>` |

Each method accepts a capacity for every set. A `StoredProcedureCommand` can also hold the expected capacities with `WithResultSetCapacities`.

## Result-set count is a contract

By default, the reader must provide exactly the number of result sets announced by the overload. Missing and additional sets cause `CaeriusContractException`; CaeriusNet fully consumes every expected set before returning.

For a procedure intentionally designed to vary its output, pass the appropriate `CaeriusResultSetCountPolicy` option. Prefer a stable procedure contract: allowing a missing or extra set should be a conscious compatibility decision, not an accidental outcome of a changed `SELECT` list.

The generic type order always follows SQL `SELECT` order. CaeriusNet cannot match sets by column name or type.

## Cache behavior

A multi-result execution has one aggregate cache identity. When cacheable and a codec is available, every set is encoded and retrieved atomically under one key; the runtime never caches sets individually. This preserves the snapshot relationship between the sets.

Next: [Table-valued parameters](/documentation/tvp) and [Caching](/documentation/cache).
