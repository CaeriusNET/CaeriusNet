namespace CaeriusNet.Commands.Transactions;

/// <summary>
///     Entry point for SQL Server transactions. Adds <c>BeginTransactionAsync</c> on
///     <see cref="ICaeriusNetDbContext" />.
/// </summary>
public static class BeginTransactionAsyncCommands
{
    /// <param name="dbContext">The database context used to open the transactional connection.</param>
    extension(ICaeriusNetDbContext dbContext)
    {
        /// <summary>
        ///     Opens a fresh <see cref="SqlConnection" /> and starts a SQL Server transaction at the
        ///     requested isolation level. The returned scope owns the connection until disposal.
        /// </summary>
        /// <param name="isolationLevel">
        ///     Isolation level. Defaults to <see cref="IsolationLevel.ReadCommitted" />, matching SQL Server's
        ///     session default.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the connection open / begin call.</param>
        /// <returns>An <see cref="ICaeriusNetTransaction" /> scope. Always wrap in <c>await using</c>.</returns>
        /// <exception cref="Exceptions.CaeriusNetSqlException">If opening the connection or beginning fails.</exception>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public ValueTask<ICaeriusNetTransaction> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default)
        {
            return CaeriusNetTransaction.BeginAsync(dbContext, isolationLevel, cancellationToken);
        }
    }
}

/// <summary>
///     Rejects nested SQL Server transactions explicitly: SQL Server only supports a single local
///     transaction per connection; use SAVEPOINTs in stored procedures for partial-rollback semantics.
/// </summary>
public static class NestedTransactionRejection
{
    /// <param name="transaction">An existing transaction scope.</param>
    extension(ICaeriusNetTransaction transaction)
    {
        /// <summary>Always throws <see cref="NotSupportedException" />.</summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public ValueTask<ICaeriusNetTransaction> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default)
        {
            _ = (transaction, isolationLevel, cancellationToken);
            throw new NotSupportedException(
                "Nested transactions are not supported. Use SAVEPOINTs in stored procedures for partial rollback.");
        }
    }
}