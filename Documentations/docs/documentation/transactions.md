# Transactions

CaeriusNet provides a lightweight transaction scope that wraps `SqlTransaction` with a thread-safe state machine, automatic rollback on dispose, cache bypass, and a parent `TX` activity for cohesive tracing. Multiple commands enlisted on the scope succeed or fail **atomically** — there are no partial commits.

## At a glance

| Feature | Behaviour |
|---|---|
| **State machine** | `Active` → `Committed` / `RolledBack` / `Poisoned` / `Disposed` |
| **Thread safety** | State transitions guarded by `Interlocked.CompareExchange` |
| **Single in-flight command** | Enforced — `SqlConnection` is not thread-safe |
| **Failure handling** | A failure poisons the scope; only `RollbackAsync` / `DisposeAsync` remain valid |
| **Cache** | Bypassed inside transactions (no dirty reads published) |
| **Auto-rollback** | An uncommitted scope rolls back on `DisposeAsync` |
| **Telemetry** | Parent `TX` span (kind = Internal) wraps every child SP span |
| **Logging** | Structured events for start, commit, rollback, and poison |

## Basic usage

### Commit example

Execute multiple commands and commit atomically:

```csharp
await using var tx = await DbContext
    .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

var spInsert = new StoredProcedureParametersBuilder("dbo", "sp_InsertOrder")
    .AddParameter("UserId", userId, SqlDbType.Int)
    .AddParameter("Amount", amount, SqlDbType.Decimal)
    .Build();

await tx.ExecuteNonQueryAsync(spInsert, ct);

var spUpdate = new StoredProcedureParametersBuilder("dbo", "sp_UpdateUserBalance")
    .AddParameter("UserId", userId, SqlDbType.Int)
    .AddParameter("Debit",  amount, SqlDbType.Decimal)
    .Build();

await tx.ExecuteNonQueryAsync(spUpdate, ct);

await tx.CommitAsync(ct);
```

### Explicit rollback

Roll back deliberately when business logic decides the work should not persist:

```csharp
await using var tx = await DbContext
    .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

var sp = new StoredProcedureParametersBuilder("dbo", "sp_ReserveInventory")
    .AddParameter("ProductId", productId, SqlDbType.Int)
    .AddParameter("Quantity",  quantity,  SqlDbType.Int)
    .Build();

var reserved = await tx.ExecuteScalarAsync<int>(sp, ct);

if (reserved < quantity)
{
    await tx.RollbackAsync(ct);
    return InventoryResult.InsufficientStock;
}

await tx.CommitAsync(ct);
return InventoryResult.Reserved;
```

### Auto-rollback on dispose

If `CommitAsync` is never called, `DisposeAsync` rolls back automatically:

```csharp
await using var tx = await DbContext
    .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

await tx.ExecuteNonQueryAsync(sp, ct);

// No CommitAsync — the transaction is rolled back when tx is disposed.
```

::: warning Always call `CommitAsync` on the success path
Auto-rollback is a safety net, not an intended control flow. Calling `CommitAsync` explicitly makes the success path obvious to the reader and to logs.
:::

## Reading inside a transaction

Query methods work inside a transaction scope. Reads see uncommitted changes made earlier in the same transaction:

```csharp
await using var tx = await DbContext
    .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

var spInsert = new StoredProcedureParametersBuilder("dbo", "sp_InsertUser")
    .AddParameter("Username", "alice", SqlDbType.NVarChar)
    .Build();

await tx.ExecuteNonQueryAsync(spInsert, ct);

var spRead = new StoredProcedureParametersBuilder("dbo", "sp_GetUser_By_Name", 1)
    .AddParameter("Username", "alice", SqlDbType.NVarChar)
    .Build();

var user = await tx.QueryAsIEnumerableAsync<UserDto>(spRead, ct);
// user contains the uncommitted row — visible within this transaction only.

await tx.CommitAsync(ct);
```

## State machine

The scope enforces a strict state machine to prevent illegal operations:

| State | Allowed operations |
|---|---|
| **Active** | `ExecuteNonQueryAsync`, `QueryAs*Async`, `CommitAsync`, `RollbackAsync`, `DisposeAsync` |
| **Committed** | `DisposeAsync` only |
| **RolledBack** | `DisposeAsync` only |
| **Poisoned** | `RollbackAsync`, `DisposeAsync` only |
| **Disposed** | None — all calls throw `ObjectDisposedException` |

```text
         ┌──────────────┐
         │    Active    │
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

When a command fails inside an `Active` transaction, the scope transitions to `Poisoned`. In this state:

- All subsequent `ExecuteNonQueryAsync` / `QueryAs*Async` calls throw immediately.
- `CommitAsync` throws — you cannot commit a poisoned transaction.
- Only `RollbackAsync` and `DisposeAsync` are valid.

This rule prevents partial commits after a failure. Retry the **entire** unit of work, do not attempt partial recovery.

```csharp
await using var tx = await DbContext
    .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

try
{
    await tx.ExecuteNonQueryAsync(sp1, ct); // succeeds
    await tx.ExecuteNonQueryAsync(sp2, ct); // throws — scope is now Poisoned
}
catch (CaeriusNetSqlException)
{
    // CommitAsync would throw here.
    await tx.RollbackAsync(ct);
}
```

## Constraints (by design)

### Single in-flight command

`SqlConnection` is not thread-safe. CaeriusNet enforces a single command at a time within a scope. Concurrent commands throw `InvalidOperationException`:

```csharp
// ❌ DO NOT — concurrent commands on the same scope
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

SQL Server does not support nested transactions on a single connection. Calling `BeginTransactionAsync` on a scope throws `NotSupportedException`. Use SQL `SAVEPOINT` inside a Stored Procedure if you need partial-rollback semantics:

```sql
CREATE PROCEDURE dbo.sp_With_Savepoint
AS
BEGIN
    SET NOCOUNT ON;
    SAVE TRANSACTION sp1;
    -- ... work ...
    IF @@ERROR <> 0
        ROLLBACK TRANSACTION sp1;
END
GO
```

### Cache bypass

CaeriusNet bypasses every cache tier (Frozen, InMemory, Redis) inside a transaction scope. This prevents uncommitted data from being published to the cache and served to other consumers. After `CommitAsync`, subsequent non-transactional reads populate the cache normally.

::: tip Cache bypass is intentional
If a cached read is critical, perform it **before** entering the transaction, or **after** committing — never inside.
:::

### Failure poisoning

Any unhandled exception from a command poisons the scope. Partial success in a transaction is unsafe — you must either `RollbackAsync` explicitly or let `DisposeAsync` auto-rollback.

## Tracing

Every scope emits a parent **`TX` span** (kind = Internal) under which all child SP spans nest. The trace remains a single cohesive workflow in the Aspire dashboard:

```text
TX  (caerius.tx.isolation_level=ReadCommitted, caerius.tx.outcome=committed)
├── SP Users.usp_Create_User  (caerius.tx=true)
└── SP Users.usp_Create_Order (caerius.tx=true)
```

| Tag | Description |
|---|---|
| `caerius.tx.isolation_level` | The SQL Server isolation level (e.g. `ReadCommitted`) |
| `caerius.tx.outcome` | `committed`, `rolled-back`, `auto-rollback`, `poisoned-auto-rollback`, `commit-failed`, `rollback-failed` |
| `caerius.tx` | `true` on every child SP span enlisted in the scope |

See [Aspire Integration — Transaction tracing](/documentation/aspire#transaction-tracing) for full details.

## Best practices

| Practice | Rationale |
|---|---|
| Always use `await using` | Guarantees `DisposeAsync` runs on every code path, including exceptions |
| Pass `CancellationToken` | Cancels in-flight SQL commands when the request is aborted |
| Keep transactions short | Long transactions hold locks and block other readers / writers |
| Pick the right `IsolationLevel` | `ReadCommitted` is the default; raise it only when you must |
| Avoid I/O inside transactions | HTTP calls, file writes, etc. extend lifetime unpredictably |
| Retry at the caller, not inside | If poisoned, create a fresh scope and retry the whole unit of work |

::: tip Isolation-level guidance
- `ReadUncommitted` — dirty reads acceptable, highest concurrency
- `ReadCommitted` — default, prevents dirty reads
- `RepeatableRead` — prevents non-repeatable reads, higher lock contention
- `Serializable` — prevents phantom reads, highest lock contention
- `Snapshot` — MVCC-based, requires database-level configuration
:::

## Error handling

SQL errors raised inside a transaction are wrapped in `CaeriusNetSqlException`:

- Original `SqlException` is preserved as `InnerException`
- The active SP span is tagged `ActivityStatusCode.Error` before the exception bubbles up

```csharp
await using var tx = await DbContext
    .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

try
{
    await tx.ExecuteNonQueryAsync(sp, ct);
    await tx.CommitAsync(ct);
}
catch (CaeriusNetSqlException ex) when (ex.InnerException is SqlException sqlEx)
{
    logger.LogError(ex, "Transaction failed on {Procedure}", ex.ProcedureName);
    await tx.RollbackAsync(ct);
}
```

## Logging events

CaeriusNet emits structured log events for the transaction lifecycle:

| Event | Level | Description |
|---|---|---|
| Transaction started | Information | Logs isolation level and connection identifier |
| Transaction committed | Information | Logs elapsed time |
| Transaction rolled back | Warning | Logs whether the rollback was explicit or automatic |
| Transaction poisoned | Error | Logs the originating exception |

---

**Next:** [Logging & Observability](/documentation/logging) — structured logging, event IDs, and OTel integration.
