# Stored Procedures

This page walks through calling Stored Procedures with CaeriusNet — from the simplest read to error handling with a graceful fallback. Every snippet uses the source generator so DTOs implement `ISpMapper<T>` automatically.

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
using CaeriusNet.Attributes.Dto;

[GenerateDto]
public sealed partial record UserDto(int UserId, Guid UserGuid);
```

## Repository skeleton

All scenarios on this page live in the same repository. It receives `ICaeriusNetDbContext` and an `ILogger` via primary-constructor DI:

```csharp
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
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

## 1. Basic read — no cache

```csharp
public async Task<IEnumerable<UserDto>> GetAllUsersAsync(CancellationToken ct)
{
    var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", ResultSetCapacity: 25)
        .Build();

    return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct);
}
```

::: tip Telemetry produced
Span: `SP Users.usp_Get_All_Users` (kind = Client, `db.system=mssql`, `caerius.rows_returned=N`).
:::

## 2. Reads with cache tiers

CaeriusNet supports three cache tiers. Add a single fluent call before `.Build()` to opt in:

::: code-group
```csharp [Frozen]
// Frozen cache — immutable snapshot, lives until the process restarts.
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 25)
    .AddFrozenCache("users:all:frozen")
    .Build();

return await DbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, ct);
```
```csharp [InMemory (TTL)]
// In-memory cache — per-process, refreshed after TTL expires.
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 25)
    .AddInMemoryCache("users:all:memory", TimeSpan.FromMinutes(1))
    .Build();

return await DbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, ct);
```
```csharp [Redis (distributed)]
// Redis distributed cache — shared across replicas, ideal for scale-out.
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 25)
    .AddRedisCache("users:all:redis", TimeSpan.FromMinutes(2))
    .Build();

return await DbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, ct);
```
:::

::: tip Cache hits skip the database
On a cache hit, **no SQL command runs and no DB span is created** — only the `caerius.cache.lookups{hit=true}` counter ticks. The Aspire dashboard accurately shows the database was not contacted.
:::

## 3. Write — scalar return

`ExecuteScalarAsync<T>` executes the SP and returns the first column of the first row — perfect for `SCOPE_IDENTITY()`, counts, or status codes:

```csharp
public async Task<int> CreateUserAsync(string userName, CancellationToken ct)
{
    var sp = new StoredProcedureParametersBuilder("Users", "usp_Create_User")
        .AddParameter("UserName", userName, SqlDbType.NVarChar)
        .Build();

    return await DbContext.ExecuteScalarAsync<int>(sp, ct) ?? 0;
}
```

## 4. Error handling — graceful fallback

Wrap calls in `try`/`catch` to degrade gracefully when a transient SQL error occurs. `CaeriusNetSqlException` always wraps the original `SqlException` in `InnerException`, and the active OTel span is already tagged `ActivityStatusCode.Error` before the exception bubbles up:

```csharp
public async Task<IEnumerable<UserDto>> GetAllUsersSafeAsync(CancellationToken ct)
{
    try
    {
        var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 25)
            .Build();

        return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct);
    }
    catch (CaeriusNetSqlException ex)
    {
        Logger.LogError(ex, "Failed to load users — returning an empty list.");
        return [];
    }
}
```

## 5. Return-type variants

`StoredProcedureParametersBuilder` is independent of the return type. Choose the collection that fits your scenario:

```csharp
// IEnumerable<T> — lazy, forward-only, lowest allocation; null on empty.
var lazy = await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct);

// IReadOnlyCollection<T> — materialized list, indexable, immutable contract.
var collection = await DbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, ct);

// ImmutableArray<T> — struct-wrapped array, ideal for cached / shared data.
var array = await DbContext.QueryAsImmutableArrayAsync<UserDto>(sp, ct);

// T? — single-row lookup; null when the result set is empty.
var first = await DbContext.FirstQueryAsync<UserDto>(sp, ct);
```

See [Reading Data](/documentation/reading-data) for guidance on choosing between them.

---

**Next:** [Table-Valued Parameters](/examples/tvp) — pass sets of identifiers into a SP without dynamic SQL.
