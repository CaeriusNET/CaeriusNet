# Transactions

CaeriusNet wraps SQL Server transactions in an `ICaeriusNetTransaction` scope obtained from `BeginTransactionAsync`. Every command executed on the scope reuses the same connection and is enlisted in the same transaction. This page walks through the three transactional outcomes you will encounter in production: **commit**, **C#-side rollback**, and **SQL-side rollback** (when the stored procedure wraps its own `BEGIN TRY / BEGIN CATCH`).

## Tracing

Each command executed in a CaeriusNet transaction emits the regular Caerius client span, including
`db.system.name=microsoft.sql_server`, `db.operation.name=EXECUTE`, the stored-procedure name and
`caerius.transactional=true`. The transaction itself deliberately does not create a synthetic parent span;
the caller's activity remains the natural parent for the workflow.

The snippets below assume these imports and an injected `ICaeriusNetDbContext DbContext`:

```csharp
using CaeriusNet;
using CaeriusNet.Abstractions;
using CaeriusNet.Commands;
using CaeriusNet.Exceptions;
using Microsoft.Data.SqlClient;
using System.Data;
```

## SQL Server objects

```sql
-- Returns the new user's identity (used inside the multi-step transaction below)
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

-- Second write chained inside the same transaction
CREATE PROCEDURE Users.usp_Create_Order
    @UserId INT,
    @Label  NVARCHAR(64),
    @Amount DECIMAL(10, 2)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO Users.Orders (UserId, Label, Amount)
    VALUES (@UserId, @Label, @Amount);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS OrderId;
END
GO

-- Self-contained transactional stored procedure using BEGIN TRY / BEGIN CATCH
CREATE PROCEDURE Users.usp_Create_User_Tx_Safe
    @UserName     NVARCHAR(64),
    @ForceFailure BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO Users.Users (UserName)
        VALUES (@UserName);

        DECLARE @newUserId INT = CAST(SCOPE_IDENTITY() AS INT);

        IF @ForceFailure = 1
            THROW 50001, N'Forced failure — rolling back.', 1;

        INSERT INTO Users.Orders (UserId, Label, Amount)
        VALUES (@newUserId, N'Welcome bonus', 0.00);

        COMMIT TRANSACTION;
        SELECT @newUserId AS UserId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;
        THROW; -- re-raise so CaeriusNet tags the span as Error
    END CATCH;
END
GO
```

## Scenario 1: Commit

Two writes are committed atomically. If anything fails before `CommitAsync`, `await using` disposes the scope and rolls back automatically.

```csharp
public async Task<int> CreateUserWithFirstOrderAsync(
    string userName,
    string orderLabel,
    decimal amount,
    CancellationToken ct)
{
    // Wrap in await using so the scope rolls back on any non-committed exit.
    await using var tx = await DbContext
        .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

    // First write — create the user
    var createUser = new StoredProcedureCommandBuilder("Users", "usp_Create_User")
        .AddParameter("UserName", userName, SqlDbType.NVarChar)
        .Build();

    var newUserId = await tx.ExecuteScalarAsync<int>(createUser, ct);

    // Second write — create their first order, using the ID from the first call
    var createOrder = new StoredProcedureCommandBuilder("Users", "usp_Create_Order")
        .AddParameter("UserId", newUserId, SqlDbType.Int)
        .AddParameter("Label",  orderLabel, SqlDbType.NVarChar)
        .AddParameter("Amount", amount,     SqlDbType.Decimal)
        .Build();

    await tx.ExecuteScalarAsync<int>(createOrder, ct);

    // Commit only after every command succeeds
    await tx.CommitAsync(ct);

    return newUserId;
}
```

The connection and SQL transaction are both released after `CommitAsync`; cache invalidations declared on a command run only after that successful commit.

## Scenario 2: C#-side rollback

The application decides to discard the work after inspecting business rules:

```csharp
public async Task DemonstrateClientSideRollbackAsync(
    string userName,
    CancellationToken ct)
{
    await using var tx = await DbContext
        .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

    var createUser = new StoredProcedureCommandBuilder("Users", "usp_Create_User")
        .AddParameter("UserName", userName, SqlDbType.NVarChar)
        .Build();

    await tx.ExecuteScalarAsync<int>(createUser, ct);

    // Imagine a business-rule check here decides we should not persist.
    await tx.RollbackAsync(ct);
    // Nothing is saved to the database.
}
```

Any invalidations deferred by commands in the transaction are discarded on rollback.

::: tip Implicit rollback on disposal
If neither `CommitAsync` nor `RollbackAsync` is called and the `await using` scope exits (even due to an exception), `DisposeAsync` rolls the transaction back and discards pending invalidations. A failed command poisons the transaction; it must be rolled back rather than committed.
:::

## Scenario 3: SQL-side rollback (`BEGIN CATCH`)

The stored procedure handles its own transaction. When `@ForceFailure = 1`, it rolls back inside `BEGIN CATCH` and re-throws. CaeriusNet wraps the resulting `SqlException` as `CaeriusNetSqlException` and marks the stored procedure span with `ActivityStatusCode.Error`:

```csharp
public async Task DemonstrateServerSideRollbackAsync(
    string userName,
    CancellationToken ct)
{
    var command = new StoredProcedureCommandBuilder("Users", "usp_Create_User_Tx_Safe")
        .AddParameter("UserName",     userName, SqlDbType.NVarChar)
        .AddParameter("ForceFailure", true,     SqlDbType.Bit)
        .Build();

    // This call throws CaeriusNetSqlException because the stored procedure re-raises.
    // The span is tagged ActivityStatusCode.Error — this is expected.
    _ = await DbContext.ExecuteScalarAsync<int>(command, ct);
}
```

Caller-side handling:

```csharp
try
{
    await usersService.DemonstrateServerSideRollbackAsync("alice", ct);
}
catch (CaeriusNetSqlException ex)
{
    // InnerException is the original SqlException.
    Logger.LogWarning(
        ex,
        "SQL-side rollback occurred: {Message}",
        ex.InnerException?.Message);

    // Apply your fallback logic here (retry, alert, return a default …)
}
```

::: warning Error span in the dashboard
The `SP Users.usp_Create_User_Tx_Safe` trace appears in red (Error) in the Aspire **Traces** tab. This is intentional. The span accurately reflects that the SQL command failed.
:::

## Commands available on `ICaeriusNetTransaction`

The transaction scope exposes the same `Execute*` / `Query*` surface as `ICaeriusNetDbContext`, with all calls enlisted in the active transaction:

| Method | Description |
|---|---|
| `ExecuteAsync` | Execute a stored procedure expected to have no business result |
| `ExecuteNonQueryAsync` | Execute a stored procedure and return the affected-row count |
| `ExecuteScalarAsync<T>` / `ExecuteScalarOrDefaultAsync<T>` | Read a required scalar or return `default` only when no row exists; SQL `NULL` still follows `T` nullability |
| `QueryFirstAsync<T>` / `QueryFirstOrDefaultAsync<T>` | Read the first row, required or optional |
| `QuerySingleAsync<T>` / `QuerySingleOrDefaultAsync<T>` | Enforce exactly one row, or zero/one row |
| `QueryAsync<T>` | Read all rows into a materialized `IEnumerable<T>` |
| `QueryReadOnlyCollectionAsync<T>` | Read all rows into a `ReadOnlyCollection<T>` |
| `QueryImmutableArrayAsync<T>` | Read all rows into an `ImmutableArray<T>` |
| `StreamAsync<T>` | Stream exactly one result set while holding the transaction command slot; fully consume it before commit |
| `Query…WithOutputsAsync` / `Execute…WithOutputsAsync` | Consume the reader then return output and return-value parameters; invalidations remain deferred to commit |
| `QueryMultiple…Async` | Materialize 2 to 10 result sets in any of the three collection shapes, with optional outputs |

::: danger Nested transactions are not supported
Calling `BeginTransactionAsync` on an `ICaeriusNetTransaction` throws `NotSupportedException`. SQL Server only supports one local transaction per connection. Use `SAVEPOINT` inside the stored procedure for partial-rollback semantics.
:::

---

**See also:** [Transactions guide](/documentation/transactions) for the full state-machine, telemetry, and best-practice reference.
