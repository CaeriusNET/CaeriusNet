namespace CaeriusNet.Abstractions;

/// <summary>
///     Represents an open SQL Server transaction scope created by <c>ICaeriusNetDbContext.BeginTransactionAsync</c>.
///     All command extensions hung off this interface (<c>ExecuteAsync</c>, <c>ExecuteScalarAsync</c>,
///     <c>FirstQueryAsync</c>, <c>QueryAsImmutableArrayAsync</c>, …) reuse the same underlying
///     <see cref="SqlConnection" /> and attach the underlying <see cref="SqlTransaction" /> to every command.
/// </summary>
/// <remarks>
///     <para>
///         <b>Always</b> wrap a transaction in <c>await using var tx = await db.BeginTransactionAsync();</c>.
///         If <see cref="CommitAsync" /> is not called before disposal, the transaction is automatically
///         rolled back.
///     </para>
///     <para>
///         A transaction is intentionally **not** an <see cref="ICaeriusNetDbContext" />: the contract of the
///         latter is "give me a fresh, caller-owned connection", which is incompatible with reusing the same
///         connection across many calls. Forcing the consumer to call <c>tx.X(...)</c> instead of <c>db.X(...)</c>
///         eliminates the foot-gun of silently running outside the transaction.
///     </para>
///     <para>
///         A transaction is **not** thread-safe. Issuing two overlapping commands on the same scope will
///         throw <see cref="InvalidOperationException" /> rather than corrupting the underlying connection.
///         Caching is **bypassed** entirely inside a transaction to avoid publishing uncommitted reads or
///         serving stale data committed under a different scope.
///     </para>
///     <para>
///         Nested transactions are not supported; SQL Server allows a single local transaction per
///         connection. Use SAVEPOINTs in stored procedures if you need partial rollback semantics.
///     </para>
/// </remarks>
public interface ICaeriusNetTransaction : IAsyncDisposable
{
    /// <summary>
    ///     <see langword="true" /> when the transaction is open and accepting commands; <see langword="false" />
    ///     after <see cref="CommitAsync" />, <see cref="RollbackAsync" />, an aborted command, or disposal.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    ///     Commits the transaction. The instance becomes inactive afterwards. Subsequent commands and
    ///     a second call to either <see cref="CommitAsync" /> or <see cref="RollbackAsync" /> throw
    ///     <see cref="InvalidOperationException" />.
    /// </summary>
    /// <exception cref="InvalidOperationException">If the transaction is no longer active.</exception>
    /// <exception cref="Exceptions.CaeriusNetSqlException">If the underlying SQL Server call fails.</exception>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Rolls the transaction back. The instance becomes inactive afterwards. Safe to call after a
    ///     command has thrown — the transaction state is poisoned and only Rollback/Dispose remain valid.
    /// </summary>
    /// <exception cref="Exceptions.CaeriusNetSqlException">If the underlying SQL Server call fails.</exception>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Internal accessor surface used by command extension methods to attach the transaction to a
///     freshly built <see cref="SqlCommand" />. Not part of the public contract.
/// </summary>
internal interface ICaeriusNetTransactionInternal : ICaeriusNetTransaction
{
    SqlConnection Connection { get; }
    SqlTransaction Transaction { get; }

    /// <summary>
    ///     Throws <see cref="InvalidOperationException" /> if the transaction is no longer active.
    ///     Sets the in-flight flag; the caller MUST invoke <see cref="ReleaseCommandSlot" /> in finally.
    /// </summary>
    void AcquireCommandSlot();

    void ReleaseCommandSlot();

    /// <summary>
    ///     Marks the transaction as poisoned after a command failure. Subsequent commands and Commit
    ///     will throw; only Rollback / Dispose remain valid.
    /// </summary>
    void Poison();
}
