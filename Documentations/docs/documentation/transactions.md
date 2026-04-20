# Transactions

CaeriusNet provides a lightweight transaction scope that wraps `SqlTransaction` with a thread-safe state machine, automatic rollback on dispose, and cache bypass. Transactions execute multiple commands atomically — either all succeed or all are rolled back.

## Overview

| Feature | Behavior |
|---|---|
| **State machine** | `Active` → `Committed` / `RolledBack` / `Poisoned` / `Disposed` |
| **Thread safety** | `Interlocked.CompareExchange` for state transitions |
| **Command enforcement** | Single in-flight command (SqlConnection is not thread-safe) |
| **Failure handling** | Failure poisons the scope — only rollback/dispose remain valid |
| **Cache** | Bypassed inside transactions (no dirty reads published) |
| **Auto-rollback** | Uncommitted scope rolls back on `DisposeAsync` |
| **Logging** | Structured events for start, commit, rollback, and poison |

## Basic usage

### Commit example

Execute multiple commands in a single transaction and commit atomically:

```csharp
await using var tx = await dbContext.BeginTransactionAsync(
    IsolationLevel.ReadCommitted, cancellationToken);

var spInsert = new StoredProcedureParametersBuilder("dbo", "sp_InsertOrder")
    .AddParameter("UserId", userId, SqlDbType.Int)
    .AddParameter("Amount", amount, SqlDbType.Decimal)
    .Build();

await tx.ExecuteNonQueryAsync(spInsert, cancellationToken);

var spUpdate = new StoredProcedureParametersBuilder("dbo", "sp_UpdateUserBalance")
    .AddParameter("UserId", userId, SqlDbType.Int)
    .AddParameter("Debit", amount, SqlDbType.Decimal)
    .Build();

await tx.ExecuteNonQueryAsync(spUpdate, cancellationToken);

await tx.CommitAsync(cancellationToken);
```

### Rollback example

Explicitly roll back when business logic requires it:

```csharp
await using var tx = await dbContext.BeginTransactionAsync(
    IsolationLevel.ReadCommitted, cancellationToken);

var sp = new StoredProcedureParametersBuilder("dbo", "sp_ReserveInventory")
    .AddParameter("ProductId", productId, SqlDbType.Int)
    .AddParameter("Quantity", quantity, SqlDbType.Int)
    .Build();

var reserved = await tx.ExecuteScalarAsync<int>(sp, cancellationToken);

if (reserved < quantity)
{
    await tx.RollbackAsync(cancellationToken);
    return InventoryResult.InsufficientStock;
}

await tx.CommitAsync(cancellationToken);
return InventoryResult.Reserved;
```

### Auto-rollback on dispose

If `CommitAsync` is never called, `DisposeAsync` automatically rolls back:

```csharp
await using var tx = await dbContext.BeginTransactionAsync(
    IsolationLevel.ReadCommitted, cancellationToken);

await tx.ExecuteNonQueryAsync(sp, cancellationToken);

// No CommitAsync — transaction is rolled back when tx is disposed.
```

::: warning Always call CommitAsync explicitly
Relying on auto-rollback is a safety net, not an intended control flow. Always call `CommitAsync` on the success path for clarity and intent.
:::

## Reading data inside transactions

Query methods work inside a transaction scope. Results reflect uncommitted changes within the same transaction:

```csharp
await using var tx = await dbContext.BeginTransactionAsync(
    IsolationLevel.ReadCommitted, cancellationToken);

var spInsert = new StoredProcedureParametersBuilder("dbo", "sp_InsertUser")
    .AddParameter("Username", "alice", SqlDbType.NVarChar)
    .Build();

await tx.ExecuteNonQueryAsync(spInsert, cancellationToken);

var spRead = new StoredProcedureParametersBuilder("dbo", "sp_GetUser_By_Name", 1)
    .AddParameter("Username", "alice", SqlDbType.NVarChar)
    .Build();

var user = await tx.QueryAsIEnumerableAsync<UserDto>(spRead, cancellationToken);
// user contains the uncommitted row — visible within this transaction.

await tx.CommitAsync(cancellationToken);
```

## State machine

The transaction scope enforces a strict state machine to prevent illegal operations:

| State | Value | Allowed operations |
|---|---|---|
| **Active** | 0 | `ExecuteNonQueryAsync`, `QueryAs*Async`, `CommitAsync`, `RollbackAsync`, `DisposeAsync` |
| **Committed** | 1 | `DisposeAsync` only |
| **RolledBack** | 2 | `DisposeAsync` only |
| **Poisoned** | 3 | `RollbackAsync`, `DisposeAsync` only |
| **Disposed** | 4 | None — all calls throw `ObjectDisposedException` |

### State transition diagram

```
         ┌──────────────┐
         │    Active     │
         └──────┬───────┘
                │
    ┌───────────┼───────────┐
    │           │           │
    ▼           ▼           ▼
Committed   RolledBack   Poisoned
    │           │           │
    └───────────┼───────────┘
                │
                ▼
           Disposed
```

### Poison state

When a command fails (throws an exception) inside an `Active` transaction, the scope transitions to `Poisoned`. In this state:

- All subsequent `ExecuteNonQueryAsync` / `QueryAs*Async` calls throw immediately.
- `CommitAsync` throws — you cannot commit a poisoned transaction.
- Only `RollbackAsync` and `DisposeAsync` are valid.

This prevents partial commits after a failure — the entire unit of work must be retried.

```csharp
await using var tx = await dbContext.BeginTransactionAsync(
    IsolationLevel.ReadCommitted, cancellationToken);

try
{
    await tx.ExecuteNonQueryAsync(sp1, cancellationToken); // succeeds
    await tx.ExecuteNonQueryAsync(sp2, cancellationToken); // throws — scope poisoned
}
catch (CaeriusNetSqlException)
{
    // tx is now Poisoned. CommitAsync would throw.
    await tx.RollbackAsync(cancellationToken);
}
```

## Constraints (by design)

### Single in-flight command

`SqlConnection` is not thread-safe. CaeriusNet enforces a single command at a time within a transaction scope. Attempting concurrent commands throws `InvalidOperationException`:

```csharp
// ❌ DO NOT — concurrent commands on the same transaction
var task1 = tx.ExecuteNonQueryAsync(sp1, ct);
var task2 = tx.ExecuteNonQueryAsync(sp2, ct); // throws InvalidOperationException
await Task.WhenAll(task1, task2);
```

```csharp
// ✅ DO — sequential commands
await tx.ExecuteNonQueryAsync(sp1, ct);
await tx.ExecuteNonQueryAsync(sp2, ct);
```

### No nested transactions

SQL Server does not support nested transactions. Use SQL `SAVEPOINT` within a stored procedure if you need partial rollback semantics:

```sql
CREATE PROCEDURE dbo.sp_WithSavepoint
AS
BEGIN
    SET NOCOUNT ON;
    SAVE TRANSACTION SavePoint1;
    -- ... work ...
    IF @@ERROR <> 0
        ROLLBACK TRANSACTION SavePoint1;
END
```

### Cache bypass

CaeriusNet bypasses all cache layers (Frozen, InMemory, Redis) inside a transaction. This prevents uncommitted (dirty) data from being published to the cache and served to other consumers.

::: tip Cache bypass is intentional
After `CommitAsync`, subsequent non-transactional reads will populate the cache normally. Design your cache keys and TTLs accordingly.
:::

### Failure poisoning

Any unhandled exception from a command execution poisons the transaction. This is deliberate — partial success in a transaction is unsafe. The poison state forces you to either:
1. Roll back explicitly with `RollbackAsync`.
2. Let `DisposeAsync` auto-rollback.

## Best practices

| Practice | Rationale |
|---|---|
| Always use `await using` | Guarantees `DisposeAsync` runs even on exception paths |
| Pass `CancellationToken` | Cancels long-running SQL commands on request abort |
| Keep transactions short | Long transactions hold locks, block other readers/writers |
| Use appropriate `IsolationLevel` | `ReadCommitted` is the default; use `Serializable` only when required |
| Avoid I/O inside transactions | HTTP calls, file writes, etc. extend transaction lifetime unpredictably |
| Retry at the caller, not inside | If poisoned, create a new transaction scope for retry |

::: warning Isolation level guidance
- `ReadUncommitted` — dirty reads acceptable, highest concurrency
- `ReadCommitted` — default, prevents dirty reads
- `RepeatableRead` — prevents non-repeatable reads, higher lock contention
- `Serializable` — prevents phantom reads, highest lock contention
- `Snapshot` — MVCC-based, requires database-level configuration
:::

## Error handling

All SQL errors inside a transaction are wrapped in `CaeriusNetSqlException`, which includes:

- The original `SqlException` as `InnerException`
- The stored procedure name that failed
- The transaction state at the time of failure

```csharp
await using var tx = await dbContext.BeginTransactionAsync(
    IsolationLevel.ReadCommitted, cancellationToken);

try
{
    await tx.ExecuteNonQueryAsync(sp, cancellationToken);
    await tx.CommitAsync(cancellationToken);
}
catch (CaeriusNetSqlException ex) when (ex.InnerException is SqlException sqlEx)
{
    // Log structured error details
    logger.LogError(ex, "Transaction failed on {Procedure}", ex.ProcedureName);
    await tx.RollbackAsync(cancellationToken);
}
```

## Logging events

CaeriusNet emits structured log events for transaction lifecycle:

| Event | Level | Description |
|---|---|---|
| Transaction started | Information | Logs isolation level and connection |
| Transaction committed | Information | Logs elapsed time |
| Transaction rolled back | Warning | Logs whether explicit or auto-rollback |
| Transaction poisoned | Error | Logs the causing exception |

---

**Next:** [Logging & Observability](/documentation/logging) — structured logging, event IDs, and telemetry integration.
