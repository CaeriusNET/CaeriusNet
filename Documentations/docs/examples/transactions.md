# Transactions

CaeriusNet wraps SQL Server transactions in an `ICaeriusNetTransaction` scope obtained from `BeginTransactionAsync`. Every command executed on the scope reuses the same connection and is enlisted in the same transaction. This page walks through the three transactional outcomes you will encounter in production: **commit**, **C#-side rollback**, and **SQL-side rollback** (when the SP wraps its own `BEGIN TRY / BEGIN CATCH`).

## Tracing

Every scope emits a parent **`TX` span** (kind = Internal) that wraps all child SP spans. The trace stays a single cohesive workflow in the Aspire dashboard:

```text
TX  (caerius.tx.isolation_level=ReadCommitted, caerius.tx.outcome=committed)
├── SP Users.usp_Create_User  (caerius.tx=true)
└── SP Users.usp_Create_Order (caerius.tx=true)
```

Without the parent `TX` span, each child SP would appear as an orphaned trace. The TX activity is what stitches them into a single unit of work.

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

-- Self-contained transactional SP using BEGIN TRY / BEGIN CATCH
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

## Scenario 1 — Commit

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
    var createUser = new StoredProcedureParametersBuilder("Users", "usp_Create_User")
        .AddParameter("UserName", userName, SqlDbType.NVarChar)
        .Build();

    var newUserId = await tx.ExecuteScalarAsync<int>(createUser, ct);

    // Second write — create their first order, using the ID from the first call
    var createOrder = new StoredProcedureParametersBuilder("Users", "usp_Create_Order")
        .AddParameter("UserId", newUserId, SqlDbType.Int)
        .AddParameter("Label",  orderLabel, SqlDbType.NVarChar)
        .AddParameter("Amount", amount,     SqlDbType.Decimal)
        .Build();

    await tx.ExecuteScalarAsync<int>(createOrder, ct);

    // Commit only after every command succeeds
    await tx.CommitAsync(ct);

    return newUserId is int id ? id : 0;
}
```

::: tip TX span outcome
`caerius.tx.outcome = committed`
:::

## Scenario 2 — C#-side rollback

The application decides to discard the work after inspecting business rules:

```csharp
public async Task DemonstrateClientSideRollbackAsync(
    string userName,
    CancellationToken ct)
{
    await using var tx = await DbContext
        .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

    var createUser = new StoredProcedureParametersBuilder("Users", "usp_Create_User")
        .AddParameter("UserName", userName, SqlDbType.NVarChar)
        .Build();

    await tx.ExecuteScalarAsync<int>(createUser, ct);

    // Imagine a business-rule check here decides we should not persist.
    await tx.RollbackAsync(ct);
    // Nothing is saved to the database.
}
```

::: tip TX span outcome
`caerius.tx.outcome = rolled-back`
:::

::: tip Implicit rollback on disposal
If neither `CommitAsync` nor `RollbackAsync` is called and the `await using` scope exits (even due to an exception), the transaction rolls back automatically in `DisposeAsync`. The TX outcome is `auto-rollback` (clean exit) or `poisoned-auto-rollback` (a command had already failed).
:::

## Scenario 3 — SQL-side rollback (`BEGIN CATCH`)

The Stored Procedure handles its own transaction. When `@ForceFailure = 1`, it rolls back inside `BEGIN CATCH` and re-throws. CaeriusNet wraps the resulting `SqlException` as `CaeriusNetSqlException` and marks the SP span with `ActivityStatusCode.Error`:

```csharp
public async Task DemonstrateServerSideRollbackAsync(
    string userName,
    CancellationToken ct)
{
    var sp = new StoredProcedureParametersBuilder("Users", "usp_Create_User_Tx_Safe")
        .AddParameter("UserName",     userName, SqlDbType.NVarChar)
        .AddParameter("ForceFailure", true,     SqlDbType.Bit)
        .Build();

    // This call throws CaeriusNetSqlException because the SP re-raises.
    // The span is tagged ActivityStatusCode.Error — this is expected.
    await DbContext.ExecuteAsync(sp, ct);
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
The `SP Users.usp_Create_User_Tx_Safe` trace appears in red (Error) in the Aspire **Traces** tab. This is **intentional** — the span accurately reflects that the SQL command failed. It is not a CaeriusNet defect.
:::

## Commands available on `ICaeriusNetTransaction`

The transaction scope exposes the same `Execute*` / `Query*` surface as `ICaeriusNetDbContext`, with all calls enlisted in the active transaction:

| Method | Description |
|---|---|
| `ExecuteAsync` | Execute a SP and ignore the result |
| `ExecuteNonQueryAsync` | Execute a SP and return the affected-row count |
| `ExecuteScalarAsync<T>` | Execute a SP and return the first column of the first row |
| `FirstQueryAsync<T>` | Read a single row and map it to a DTO |
| `QueryAsIEnumerableAsync<T>` | Read all rows into an `IEnumerable<T>` |
| `QueryAsReadOnlyCollectionAsync<T>` | Read all rows into a `ReadOnlyCollection<T>` |
| `QueryAsImmutableArrayAsync<T>` | Read all rows into an `ImmutableArray<T>` |

::: danger Nested transactions are not supported
Calling `BeginTransactionAsync` on an `ICaeriusNetTransaction` throws `NotSupportedException`. SQL Server only supports one local transaction per connection — use `SAVEPOINT` inside the SP for partial-rollback semantics.
:::

---

**See also:** [Transactions guide](/documentation/transactions) for the full state-machine, telemetry, and best-practice reference.
