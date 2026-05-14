# Writing data

CaeriusNet provides three write commands for executing stored procedures that modify data. All write commands are asynchronous, `CancellationToken`-aware, and available inside a transaction scope.

## Overview

| Method | Returns | When to use |
|---|---|---|
| `ExecuteNonQueryAsync` | `int` (rows affected) | INSERT / UPDATE / DELETE — when you need a row count |
| `ExecuteAsync` | *void* (`ValueTask`) | Fire-and-forget writes — row count not needed |
| `ExecuteScalarAsync<T>` | `T?` | SELECT scalar — `SCOPE_IDENTITY`, `COUNT`, status code, etc. |

## A representative write stored procedure

```sql
CREATE PROCEDURE dbo.sp_UpdateUserAge_By_Guid
    @Guid UNIQUEIDENTIFIER,
    @Age  TINYINT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.Users
        SET    Age = @Age
        WHERE  Guid = @Guid;

        IF @@TRANCOUNT > 0
            COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;  -- re-raise so the stored procedure span is tagged Error
    END CATCH;
END
GO
```

## `ExecuteNonQueryAsync` — affected rows

Use when you need to know how many rows were affected (for example to validate an update):

```csharp
public async Task<int> UpdateUserAgeAsync(
    Guid guid, byte age, CancellationToken ct)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "sp_UpdateUserAge_By_Guid")
        .AddParameter("Guid", guid, SqlDbType.UniqueIdentifier)
        .AddParameter("Age",  age,  SqlDbType.TinyInt)
        .Build();

    return await DbContext.ExecuteNonQueryAsync(sp, ct);
}
```

## `ExecuteAsync` — fire and forget

Use when the affected-row count is irrelevant. Slightly leaner than `ExecuteNonQueryAsync`:

```csharp
public async Task DeleteUserAsync(Guid guid, CancellationToken ct)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "sp_DeleteUser_By_Guid")
        .AddParameter("Guid", guid, SqlDbType.UniqueIdentifier)
        .Build();

    await DbContext.ExecuteAsync(sp, ct);
}
```

## `ExecuteScalarAsync<T>` — scalar return

Use when the stored procedure returns a single scalar value, such as a new identity, a count, or a status code.

```sql
CREATE PROCEDURE dbo.sp_InsertUser_Return_Id
    @Username NVARCHAR(100),
    @Age      TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Users (Username, Age)
    VALUES (@Username, @Age);
    SELECT CAST(SCOPE_IDENTITY() AS INT);
END
GO
```

```csharp
public async Task<int?> InsertUserAsync(
    string username, byte age, CancellationToken ct)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "sp_InsertUser_Return_Id")
        .AddParameter("Username", username, SqlDbType.NVarChar)
        .AddParameter("Age",      age,      SqlDbType.TinyInt)
        .Build();

    return await DbContext.ExecuteScalarAsync<int>(sp, ct);
}
```

## Capacity for write operations

Write commands do not return result sets, so the `resultSetCapacity` argument has no effect — leave it at the default:

```csharp
new StoredProcedureParametersBuilder("dbo", "sp_DeleteUser_By_Guid"); // capacity ignored
```

## Multiple parameters

Chain as many `.AddParameter()` calls as needed. Each maps to a named stored procedure parameter:

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_InsertAuditLog")
    .AddParameter("UserId",     userId,           SqlDbType.Int)
    .AddParameter("Action",     action,           SqlDbType.NVarChar)
    .AddParameter("OccurredAt", DateTime.UtcNow,  SqlDbType.DateTime2)
    .Build();

await DbContext.ExecuteAsync(sp, ct);
```

## Combining a TVP with a write

`AddTvpParameter` works on write commands too — perfect for bulk inserts or deletes by ID set:

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_DeleteUsers_By_Ids")
    .AddTvpParameter("Ids", userIds)
    .Build();

var deleted = await DbContext.ExecuteNonQueryAsync(sp, ct);
```

See [table-valued parameters](/documentation/tvp) for the full TVP guide.

## Writes inside a transaction

All three commands are also exposed on `ICaeriusNetTransaction` for atomic multi-statement units of work. See [Transactions](/documentation/transactions).

```csharp
await using var tx = await DbContext.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

await tx.ExecuteNonQueryAsync(spDebit,  ct);
await tx.ExecuteNonQueryAsync(spCredit, ct);
await tx.CommitAsync(ct);
```

---

**Next:** [Multiple result sets](/documentation/multi-results) - fetch two to five result sets in a single round trip.
