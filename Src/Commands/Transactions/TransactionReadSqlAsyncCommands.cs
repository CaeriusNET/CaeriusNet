namespace CaeriusNet.Commands.Transactions;

/// <summary>
///     Asynchronous read commands scoped to an <see cref="ICaeriusNetTransaction" />. Each call reuses
///     the transaction's open <see cref="SqlConnection" /> and attaches the underlying
///     <see cref="SqlTransaction" /> to every command. Caching is **bypassed** entirely to avoid
///     publishing uncommitted reads or serving stale data committed under a different scope.
/// </summary>
public static class TransactionReadSqlAsyncCommands
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ICaeriusNetTransactionInternal AsInternal(ICaeriusNetTransaction transaction)
    {
        return transaction as ICaeriusNetTransactionInternal
               ?? throw new InvalidOperationException(
                   "ICaeriusNetTransaction implementations must derive from the framework's CaeriusNetTransaction.");
    }

    /// <param name="transaction">The transaction whose connection / scope is reused.</param>
    extension(ICaeriusNetTransaction transaction)
    {
        /// <inheritdoc cref="SimpleReadSqlAsyncCommands" />
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<TResultSet?> FirstQueryAsync<TResultSet>(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
            where TResultSet : class, ISpMapper<TResultSet>
        {
            var tx = AsInternal(transaction);
            tx.AcquireCommandSlot();
            try
            {
                return await SqlCommandHelperTx.ScalarQueryTxAsync<TResultSet>(
                    spParameters, tx.Connection, tx.Transaction, cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException ex)
            {
                tx.Poison();
                throw new CaeriusNetSqlException(
                    $"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
            }
            finally
            {
                tx.ReleaseCommandSlot();
            }
        }

        /// <inheritdoc cref="SimpleReadSqlAsyncCommands" />
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<ReadOnlyCollection<TResultSet>> QueryAsReadOnlyCollectionAsync<TResultSet>(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
            where TResultSet : class, ISpMapper<TResultSet>
        {
            var tx = AsInternal(transaction);
            tx.AcquireCommandSlot();
            try
            {
                var results = await SqlCommandHelperTx.ResultSetAsReadOnlyCollectionTxAsync<TResultSet>(
                    spParameters, tx.Connection, tx.Transaction, cancellationToken).ConfigureAwait(false);

                return results.Count == 0
                    ? EmptyCollections.ReadOnlyCollection<TResultSet>()
                    : results;
            }
            catch (SqlException ex)
            {
                tx.Poison();
                throw new CaeriusNetSqlException(
                    $"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
            }
            finally
            {
                tx.ReleaseCommandSlot();
            }
        }

        /// <inheritdoc cref="SimpleReadSqlAsyncCommands" />
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<IEnumerable<TResultSet>> QueryAsIEnumerableAsync<TResultSet>(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
            where TResultSet : class, ISpMapper<TResultSet>
        {
            return await transaction.QueryAsReadOnlyCollectionAsync<TResultSet>(spParameters, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc cref="SimpleReadSqlAsyncCommands" />
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<ImmutableArray<TResultSet>> QueryAsImmutableArrayAsync<TResultSet>(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
            where TResultSet : class, ISpMapper<TResultSet>
        {
            var tx = AsInternal(transaction);
            tx.AcquireCommandSlot();
            try
            {
                return await SqlCommandHelperTx.ResultSetAsImmutableArrayTxAsync<TResultSet>(
                    spParameters, tx.Connection, tx.Transaction, cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException ex)
            {
                tx.Poison();
                throw new CaeriusNetSqlException(
                    $"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
            }
            finally
            {
                tx.ReleaseCommandSlot();
            }
        }
    }
}