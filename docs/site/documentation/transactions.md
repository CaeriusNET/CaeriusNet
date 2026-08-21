# Transactions

`BeginTransactionAsync` creates one SQL connection and one SQL transaction for the entire CaeriusNet transaction. The scope rejects concurrent commands, bypasses cache reads and defers declared invalidations until a successful commit.

```csharp
await using var transaction = await database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

await transaction.ExecuteNonQueryAsync(debitCommand, ct);
await transaction.ExecuteNonQueryAsync(creditCommand, ct);

await transaction.CommitAsync(ct);
```

If `CommitAsync` is omitted, disposal rolls back. After rollback, invalidations are abandoned. A failed operation poisons the scope; roll back or dispose rather than running more commands.

Cancellation is observed before `CommitAsync` or `RollbackAsync` starts. Once CaeriusNet has reserved that
terminal operation, it completes the SQL exchange without the caller token. This prevents an HTTP request
abort that races an already-sent `COMMIT` from reporting a false cancellation or skipping the required cache
invalidations. If the token was already cancelled, `OperationCanceledException` is thrown and the transaction
remains active so it can be rolled back (or disposed) explicitly.

After the durable SQL commit succeeds, declared cache-tag invalidations run with `CancellationToken.None`. If one fails,
`CaeriusCacheInvalidationException.SqlWasCommitted` is `true`: the database change is real and a blind retry of a
non-idempotent procedure can duplicate it. Reconcile/repair the cache instead.

## Constraints

- Do not use a transaction concurrently from several tasks.
- Do not expect cache read-through or population inside the transaction.
- Do not retain an active transaction longer than the unit of work it protects.
- Command and stream cancellation remains `OperationCanceledException` rather than a wrapped SQL exception. A
  cancelled or otherwise incomplete stream poisons the transaction, because SQL Server may still have unread
  protocol data. `CommitAsync` and `RollbackAsync` use the terminal-operation cancellation rule described above.

The transaction extension surface mirrors the primary context for cardinality reads, materialized reads, scalars,
writes, SQL outputs and multi-result procedures (arities 2–10 for `IEnumerable<T>`, `ReadOnlyCollection<T>` and
`ImmutableArray<T>`). `StreamAsync<T>` is supported for one result set: it keeps the transaction command slot until
the enumerator completes, so consume it fully before calling `CommitAsync`. Stopping early, cancellation, a mapping
failure, or an unexpected extra result set poisons the scope; roll it back instead of committing.
