using System.Diagnostics;

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

    /// <param name="transaction">Transaction whose connection and scope are reused.</param>
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
            var logger = LoggerProvider.GetLogger();
            tx.AcquireCommandSlot();
            var startTimestamp = Stopwatch.GetTimestamp();
            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogExecutingProcedure(
                    spParameters.SchemaName,
                    spParameters.ProcedureName,
                    spParameters.GetParametersSpan().Length);

            try
            {
                var result = await SqlCommandHelperTx.ScalarQueryTxAsync<TResultSet>(
                    spParameters, tx.Connection, tx.Transaction, cancellationToken).ConfigureAwait(false);

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                        result is null ? 0 : 1);

                return result;
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
            var logger = LoggerProvider.GetLogger();
            tx.AcquireCommandSlot();
            var startTimestamp = Stopwatch.GetTimestamp();
            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogExecutingProcedure(
                    spParameters.SchemaName,
                    spParameters.ProcedureName,
                    spParameters.GetParametersSpan().Length);

            try
            {
                var results = await SqlCommandHelperTx.ResultSetAsReadOnlyCollectionTxAsync<TResultSet>(
                    spParameters, tx.Connection, tx.Transaction, cancellationToken).ConfigureAwait(false);

                results = results.Count == 0
                    ? EmptyCollections.ReadOnlyCollection<TResultSet>()
                    : results;

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                        results.Count);

                return results;
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
            var logger = LoggerProvider.GetLogger();
            tx.AcquireCommandSlot();
            var startTimestamp = Stopwatch.GetTimestamp();
            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogExecutingProcedure(
                    spParameters.SchemaName,
                    spParameters.ProcedureName,
                    spParameters.GetParametersSpan().Length);

            try
            {
                var results = await SqlCommandHelperTx.ResultSetAsImmutableArrayTxAsync<TResultSet>(
                    spParameters, tx.Connection, tx.Transaction, cancellationToken).ConfigureAwait(false);

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                        results.Length);

                return results;
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