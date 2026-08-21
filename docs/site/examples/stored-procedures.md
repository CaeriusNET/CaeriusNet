# Stored procedures

This page walks through calling stored procedures with CaeriusNet v12, from the simplest read to error handling with a graceful fallback. Every snippet uses the source generator so DTOs implement `ISpMapper<T>` automatically.

## SQL Server objects

```sql
-- Schema and table (already created by init.sql)
-- CREATE SCHEMA Users;
-- CREATE TABLE Users.Users (
--     UserId    INT IDENTITY PRIMARY KEY,
--     UserGuid  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
--     UserName  NVARCHAR(64)     NOT NULL
-- );

-- Read all users
CREATE PROCEDURE Users.usp_Get_All_Users
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId, UserGuid
    FROM   Users.Users
    ORDER BY UserId;
END
GO

-- Insert a user, return its identity
CREATE PROCEDURE Users.usp_Create_User
    @UserName NVARCHAR(64) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @name NVARCHAR(64) = COALESCE(@UserName, CONCAT(N'demo-', NEWID()));

    INSERT INTO Users.Users (UserName)
    VALUES (@name);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS UserId;
END
GO
```

## DTO

```csharp
using CaeriusNet;

[GenerateDto]
public sealed partial record UserDto(int UserId, Guid UserGuid);
```

## Repository skeleton

All scenarios on this page live in the same repository. It receives `ICaeriusNetDbContext` and an `ILogger` via primary-constructor DI:

```csharp
using CaeriusNet.Abstractions;
using CaeriusNet.Commands;
using CaeriusNet.Exceptions;
using Microsoft.Extensions.Logging;
using System.Data;

public sealed record UsersRepository(
    ICaeriusNetDbContext   DbContext,
    ILogger<UsersRepository> Logger)
    : IUsersRepository
{
    // ... methods follow ...
}
```

## 1. Basic read without cache

```csharp
public async Task<IEnumerable<UserDto>> GetAllUsersAsync(CancellationToken ct)
{
    var command = new StoredProcedureCommandBuilder("Users", "usp_Get_All_Users")
        .WithResultSetCapacities(25)
        .Build();

    return await DbContext.QueryAsync<UserDto>(command, cancellationToken: ct);
}
```

::: tip Telemetry produced
Span: `Users.usp_Get_All_Users` (kind = Client, `db.system.name=microsoft.sql_server`, `db.operation.name=EXECUTE`).
:::

## 2. Reads with cache tiers

CaeriusNet supports three cache tiers. The cache identity is derived centrally from the application namespace, stored-procedure contract, result shape and canonical parameters; applications do not supply ad-hoc cache keys.

::: code-group
```csharp [Frozen]
// Frozen cache — an explicitly replaced immutable snapshot; a miss never rebuilds it.
var command = new StoredProcedureCommandBuilder("Users", "usp_Get_All_Users")
    .UseFrozenCache()
    .Build();

return await DbContext.QueryReadOnlyCollectionAsync<UserDto>(command, cancellationToken: ct);
```
```csharp [InMemory (TTL)]
// In-memory cache — local TTL and single-flight population.
var command = new StoredProcedureCommandBuilder("Users", "usp_Get_All_Users")
    .UseInMemoryCache(TimeSpan.FromMinutes(1), slidingExpiration: true)
    .Build();

return await DbContext.QueryReadOnlyCollectionAsync<UserDto>(command, cancellationToken: ct);
```
```csharp [Redis (distributed)]
// Redis is opt-in through CaeriusNet.Redis and needs a typed cache codec.
// [GenerateDto] emits UserDtoCaeriusCacheCodec and this registration extension.
services.AddUserDtoCaeriusCodec();

var command = new StoredProcedureCommandBuilder("Users", "usp_Get_All_Users")
    .UseRedisCache(TimeSpan.FromMinutes(2))
    .Build();

return await DbContext.QueryReadOnlyCollectionAsync<UserDto>(command, cancellationToken: ct);
```
:::

::: tip Cache hits skip the database
On a cache hit, **no SQL command runs and no database client span is created**. Cache metrics use only low-cardinality tier and outcome dimensions; they do not include keys or values.
:::

## 3. Write with scalar return

`ExecuteScalarAsync<T>` executes the stored procedure and returns the first column of the first row. Use it for `SCOPE_IDENTITY()`, counts, or status codes:

```csharp
public async Task<int> CreateUserAsync(string userName, CancellationToken ct)
{
    var command = new StoredProcedureCommandBuilder("Users", "usp_Create_User")
        .AddParameter("UserName", userName, SqlDbType.NVarChar)
        .Build();

    return await DbContext.ExecuteScalarAsync<int>(command, ct);
}
```

## 4. Error handling with fallback

Wrap calls in `try`/`catch` to degrade gracefully when a transient SQL error occurs. `CaeriusNetSqlException` always wraps the original `SqlException` in `InnerException`, and the active OTel span is already tagged `ActivityStatusCode.Error` before the exception bubbles up:

```csharp
public async Task<IEnumerable<UserDto>> GetAllUsersSafeAsync(CancellationToken ct)
{
    try
    {
        var command = new StoredProcedureCommandBuilder("Users", "usp_Get_All_Users")
            .WithResultSetCapacities(25)
            .Build();

        return await DbContext.QueryAsync<UserDto>(command, cancellationToken: ct);
    }
    catch (CaeriusNetSqlException ex)
    {
        Logger.LogError(ex, "Failed to load users — returning an empty list.");
        return [];
    }
}
```

## 5. Return-type variants

`StoredProcedureCommand` is independent of the return type. Choose the collection that fits your scenario:

```csharp
// IEnumerable<T>: materialized sequence; empty sequence on empty.
var materialized = await DbContext.QueryAsync<UserDto>(command, cancellationToken: ct);

// IReadOnlyCollection<T>: materialized list, indexable, immutable contract.
var collection = await DbContext.QueryReadOnlyCollectionAsync<UserDto>(command, cancellationToken: ct);

// ImmutableArray<T>: ideal for cached or shared data.
var array = await DbContext.QueryImmutableArrayAsync<UserDto>(command, cancellationToken: ct);

// T?: single-row lookup; null when the result set is empty.
var first = await DbContext.QueryFirstOrDefaultAsync<UserDto>(command, ct);
```

See [Reading data](/documentation/reading-data) for guidance on choosing a result shape.

---

**Next:** [table-valued parameters](/examples/tvp) - pass sets of identifiers into a stored procedure without dynamic SQL.
