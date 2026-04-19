# Writing Data

CaeriusNet provides three write commands for executing Stored Procedures that modify data. All are async-only and accept a `CancellationToken`.

## Write commands overview

| Method | Return | When to use |
|---|---|---|
| `ExecuteNonQueryAsync` | `int` (rows affected) | INSERT / UPDATE / DELETE — need row count |
| `ExecuteAsync` | `void` | Fire-and-forget writes — row count not needed |
| `ExecuteScalarAsync<T>` | `T?` | SELECT scalar — SCOPE_IDENTITY, COUNT, etc. |

## Setting up a write Stored Procedure

```sql
CREATE PROCEDURE dbo.sp_UpdateUserAge_By_Guid
    @Guid   UNIQUEIDENTIFIER,
    @Age    TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

    BEGIN TRY
        BEGIN TRANSACTION
            UPDATE dbo.Users
            SET Age = @Age
            WHERE Guid = @Guid;
        IF @@TRANCOUNT > 0
            COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
    END CATCH
END
```

## `ExecuteNonQueryAsync` — row count

Use when you need to know how many rows were affected (e.g., to validate an update):

```csharp
public async Task<int> UpdateUserAgeAsync(
    Guid guid, byte age, CancellationToken cancellationToken)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "sp_UpdateUserAge_By_Guid")
        .AddParameter("Guid", guid, SqlDbType.UniqueIdentifier)
        .AddParameter("Age", age, SqlDbType.TinyInt)
        .Build();

    return await DbContext.ExecuteNonQueryAsync(sp, cancellationToken);
}
```

## `ExecuteAsync` — fire and forget

Use when the result count is irrelevant. Slightly leaner than `ExecuteNonQueryAsync`:

```csharp
public async Task DeleteUserAsync(Guid guid, CancellationToken cancellationToken)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "sp_DeleteUser_By_Guid")
        .AddParameter("Guid", guid, SqlDbType.UniqueIdentifier)
        .Build();

    await DbContext.ExecuteAsync(sp, cancellationToken);
}
```

## `ExecuteScalarAsync<T>` — scalar return

Use when the Stored Procedure returns a single scalar value — for example a new identity, a count, or a status code:

```sql
CREATE PROCEDURE dbo.sp_InsertUser_Return_Id
    @Username   NVARCHAR(100),
    @Age        TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Users (Username, Age)
    VALUES (@Username, @Age);
    SELECT SCOPE_IDENTITY();
END
```

```csharp
public async Task<int?> InsertUserAsync(
    string username, byte age, CancellationToken cancellationToken)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "sp_InsertUser_Return_Id")
        .AddParameter("Username", username, SqlDbType.NVarChar)
        .AddParameter("Age", age, SqlDbType.TinyInt)
        .Build();

    return await DbContext.ExecuteScalarAsync<int>(sp, cancellationToken);
}
```

## Capacity for write operations

Write operations do not return a result set. You can omit the capacity argument (it defaults to `16`) or explicitly pass any small value — it has no performance impact for non-query operations:

```csharp
// Capacity is irrelevant for writes — default 16 is fine
new StoredProcedureParametersBuilder("dbo", "sp_DeleteUser_By_Guid")
```

## Multiple parameters

Chain as many `.AddParameter()` calls as needed. Each call maps to a named SP parameter:

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_InsertAuditLog")
    .AddParameter("UserId", userId, SqlDbType.Int)
    .AddParameter("Action", action, SqlDbType.NVarChar)
    .AddParameter("OccurredAt", DateTime.UtcNow, SqlDbType.DateTime2)
    .Build();

await DbContext.ExecuteAsync(sp, cancellationToken);
```

## Combining TVP with writes

You can use `AddTvpParameter` on write operations too — for bulk inserts or deletes by ID set:

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_DeleteUsers_By_Ids")
    .AddTvpParameter("Ids", userIds)
    .Build();

var deleted = await DbContext.ExecuteNonQueryAsync(sp, cancellationToken);
```

---

**Next:** [Multiple Result Sets](/documentation/multi-results) — fetch two to five result sets in a single round-trip.
