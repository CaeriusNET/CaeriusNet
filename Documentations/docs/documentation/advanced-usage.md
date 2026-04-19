# Advanced Usage

This page covers patterns for combining CaeriusNet features in real-world scenarios: mixing TVP with scalar parameters, applying caching selectively, and structuring repositories for maximum reuse.

## TVP + regular parameters

Combine `AddTvpParameter` and `AddParameter` to send a structured row set alongside scalar filters:

```sql
CREATE PROCEDURE dbo.sp_GetUsers_By_Tvp_Ids_And_Age
    @Ids  dbo.tvp_int READONLY,
    @Age  INT
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
[GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
public sealed partial record UserIdTvp(int Id);

public async Task<IEnumerable<UserDto>> GetUsersByIdsAndAgeAsync(
    IEnumerable<UserDto> candidates, int minAge, CancellationToken ct)
{
    var tvpItems = candidates.Take(4242).Select(u => new UserIdTvp(u.Id)).ToList();
    if (tvpItems.Count == 0) return [];

    var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Tvp_Ids_And_Age", 4242)
        .AddTvpParameter("Ids", tvpItems)
        .AddParameter("Age", minAge, SqlDbType.Int)
        .Build();

    return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct) ?? [];
}
```

## Conditional caching

Apply different cache strategies depending on input. For example, cache all-users queries but skip caching for filtered queries:

```csharp
public async Task<IEnumerable<UserDto>> GetUsersAsync(
    int? minAge, CancellationToken ct)
{
    var builder = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Age", 250);

    if (minAge.HasValue)
        builder.AddParameter("Age", minAge.Value, SqlDbType.Int);
    else
        builder.AddFrozenCache("users:all");  // only cache the unfiltered result

    return await DbContext.QueryAsIEnumerableAsync<UserDto>(builder.Build(), ct) ?? [];
}
```

## Multiple result sets with TVP

Multi-result SP calls support parameters including TVPs. Pass the `StoredProcedureParameters` to any `QueryMultiple*` method:

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_Get_Dashboard_Data", 512)
    .AddTvpParameter("FilterIds", filterIds)
    .AddParameter("MaxAge", maxAge, SqlDbType.Int)
    .AddRedisCache($"dashboard:max:{maxAge}", TimeSpan.FromMinutes(3))
    .Build();

var (users, orders) = await DbContext
    .QueryMultipleIEnumerableAsync<UserDto, OrderDto>(sp, ct);
```

## Scalar return after write

Use `ExecuteScalarAsync<T>` when the SP returns a single value, such as a newly inserted identity:

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_InsertUser_Return_Id")
    .AddParameter("Username", username, SqlDbType.NVarChar)
    .AddParameter("Age", age, SqlDbType.TinyInt)
    .Build();

var newId = await DbContext.ExecuteScalarAsync<int>(sp, cancellationToken);
```

## Performance notes

| Technique | Benefit |
|---|---|
| Pre-sized `resultSetCapacity` | Avoids `List<T>` resizing for large result sets |
| `QueryAsImmutableArrayAsync` | Struct-backed, pool-allocated — zero extra copies |
| TVP `SqlDataRecord` reuse | Single instance per call regardless of row count |
| `AddFrozenCache` | Eliminates DB round-trip entirely for static data |
| `CancellationToken` propagation | Cancels in-progress SQL command on request abort |

---

**See also:** [Reading Data](/documentation/reading-data) · [Writing Data](/documentation/writing-data) · [Table-Valued Parameters](/documentation/tvp) · [Multiple Result Sets](/documentation/multi-results)
