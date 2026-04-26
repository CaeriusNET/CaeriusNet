# Advanced Usage

This page collects patterns that combine multiple CaeriusNet features in real-world scenarios — TVPs alongside scalar parameters, conditional caching, multi-result-set calls with TVP filters, and fine-grained transaction handling.

## TVPs combined with scalar parameters

Combine `AddTvpParameter` and `AddParameter` to send a structured row set alongside scalar filters:

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
[GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
public sealed partial record UserIdTvp(int Id);

public async Task<IEnumerable<UserDto>> GetUsersByIdsAndAgeAsync(
    IReadOnlyCollection<int> userIds, int minAge, CancellationToken ct)
{
    if (userIds.Count == 0) return [];

    var tvpItems = userIds.Select(id => new UserIdTvp(id));

    var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Tvp_Ids_And_Age", capacity: 4096)
        .AddTvpParameter("Ids", tvpItems)
        .AddParameter("Age", minAge, SqlDbType.Int)
        .Build();

    return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct) ?? [];
}
```

## Conditional caching

Apply different cache strategies depending on input. For example, cache the unfiltered query but skip caching for filtered queries:

```csharp
public async Task<IEnumerable<UserDto>> GetUsersAsync(
    int? minAge, CancellationToken ct)
{
    var builder = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Age", 250);

    if (minAge.HasValue)
        builder.AddParameter("Age", minAge.Value, SqlDbType.Int);
    else
        builder.AddFrozenCache("users:all"); // only cache the unfiltered result

    return await DbContext.QueryAsIEnumerableAsync<UserDto>(builder.Build(), ct) ?? [];
}
```

## Multiple result sets with a TVP filter

Multi-result SP calls accept the same parameters as single-result calls — including TVPs and any cache tier:

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_Get_Dashboard_Data", 512)
    .AddTvpParameter("FilterIds", filterIds)
    .AddParameter("MaxAge", maxAge, SqlDbType.Int)
    .AddRedisCache($"dashboard:max:{maxAge}", TimeSpan.FromMinutes(3))
    .Build();

var (users, orders) = await DbContext
    .QueryMultipleIEnumerableAsync<UserDto, OrderDto>(sp, ct);
```

## Scalar return after a write

Use `ExecuteScalarAsync<T>` when the SP returns a single value — for example a newly inserted identity:

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_InsertUser_Return_Id")
    .AddParameter("Username", username, SqlDbType.NVarChar)
    .AddParameter("Age",      age,      SqlDbType.TinyInt)
    .Build();

var newId = await DbContext.ExecuteScalarAsync<int>(sp, ct);
```

## Performance levers

| Technique | Benefit |
|---|---|
| Pre-sized `resultSetCapacity` | Avoids `List<T>` resizing for large result sets |
| `QueryAsImmutableArrayAsync` | Struct-backed, pool-allocated — zero extra copies |
| TVP `SqlDataRecord` reuse | Single instance per call regardless of row count |
| `AddFrozenCache` | Eliminates the DB round-trip entirely for static data |
| `CancellationToken` propagation | Cancels in-progress SQL commands when the request is aborted |

## Transaction patterns

### Multi-command unit of work

Execute multiple operations atomically — all succeed or all roll back:

```csharp
await using var tx = await DbContext
    .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

var spOrder = new StoredProcedureParametersBuilder("dbo", "sp_InsertOrder")
    .AddParameter("UserId", userId, SqlDbType.Int)
    .AddParameter("Total",  total,  SqlDbType.Decimal)
    .Build();

await tx.ExecuteNonQueryAsync(spOrder, ct);

var spInventory = new StoredProcedureParametersBuilder("dbo", "sp_DecrementStock")
    .AddParameter("ProductId", productId, SqlDbType.Int)
    .AddParameter("Quantity",  qty,        SqlDbType.Int)
    .Build();

await tx.ExecuteNonQueryAsync(spInventory, ct);

var spBalance = new StoredProcedureParametersBuilder("dbo", "sp_DebitUserBalance")
    .AddParameter("UserId", userId, SqlDbType.Int)
    .AddParameter("Amount", total,  SqlDbType.Decimal)
    .Build();

await tx.ExecuteNonQueryAsync(spBalance, ct);

await tx.CommitAsync(ct);
```

### Conditional rollback

Roll back based on business logic without raising an exception:

```csharp
await using var tx = await DbContext
    .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

var spCheck = new StoredProcedureParametersBuilder("dbo", "sp_GetAvailableStock", 1)
    .AddParameter("ProductId", productId, SqlDbType.Int)
    .Build();

var stock = await tx.ExecuteScalarAsync<int>(spCheck, ct);

if (stock < requestedQuantity)
{
    await tx.RollbackAsync(ct);
    return OrderResult.InsufficientStock;
}

var spReserve = new StoredProcedureParametersBuilder("dbo", "sp_ReserveStock")
    .AddParameter("ProductId", productId,         SqlDbType.Int)
    .AddParameter("Quantity",  requestedQuantity, SqlDbType.Int)
    .Build();

await tx.ExecuteNonQueryAsync(spReserve, ct);
await tx.CommitAsync(ct);
return OrderResult.Reserved;
```

### Poison-state handling

When a command fails, the scope poisons. Roll back and let the caller retry the entire unit of work:

```csharp
await using var tx = await DbContext
    .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

try
{
    await tx.ExecuteNonQueryAsync(sp1, ct);
    await tx.ExecuteNonQueryAsync(sp2, ct); // may throw — poisons the scope
    await tx.CommitAsync(ct);
}
catch (CaeriusNetSqlException ex)
{
    logger.LogWarning(ex, "Transaction poisoned by {Procedure}", ex.ProcedureName);
    await tx.RollbackAsync(ct);
    throw; // retry at the caller — never partially recover
}
```

## Logging configuration in detail

### Setting up the logger with DI

Configure CaeriusNet logging during application startup. The logger must be set before any database operations are performed:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddFilter("CaeriusNet",       LogLevel.Information);
    logging.AddFilter("CaeriusNet.Cache", LogLevel.Warning);
});

CaeriusNetBuilder
    .Create(builder.Services)
    .WithSqlServer(connectionString)
    .Build();

var app = builder.Build();

// LoggerProvider is wired automatically when ILoggerFactory is in DI;
// the explicit call below is only needed if you build without DI.
LoggerProvider.SetLogger(app.Services.GetRequiredService<ILoggerFactory>());
```

### Filtering by category in `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default":              "Information",
      "CaeriusNet.Cache":     "Warning",
      "CaeriusNet.Commands":  "Debug"
    }
  }
}
```

---

**See also:** [Reading Data](/documentation/reading-data) · [Writing Data](/documentation/writing-data) · [Table-Valued Parameters](/documentation/tvp) · [Multiple Result Sets](/documentation/multi-results) · [Transactions](/documentation/transactions) · [Logging](/documentation/logging)
