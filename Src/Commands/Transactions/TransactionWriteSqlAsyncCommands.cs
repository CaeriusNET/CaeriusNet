using System.Diagnostics;

namespace CaeriusNet.Commands.Transactions;

/// <summary>
///     Asynchronous write commands scoped to an <see cref="ICaeriusNetTransaction" />. They share the
///     transaction's connection and attach the underlying <see cref="SqlTransaction" /> to every command.
/// </summary>
public static class TransactionWriteSqlAsyncCommands
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
        /// <inheritdoc cref="WriteSqlAsyncCommands" />
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<T?> ExecuteScalarAsync<T>(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
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
                var result = await SqlCommandHelperTx.ExecuteCommandTxAsync(
                    spParameters, tx.Connection, tx.Transaction,
                    async command =>
                    {
                        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                        return result is DBNull ? default : (T?)result;
                    }, cancellationToken).ConfigureAwait(false);

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureScalarCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);

                return result;
            }
            catch (CaeriusNetSqlException)
            {
                tx.Poison();
                throw;
            }
            finally
            {
                tx.ReleaseCommandSlot();
            }
        }

        /// <inheritdoc cref="WriteSqlAsyncCommands" />
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<int> ExecuteNonQueryAsync(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
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
                var rowsAffected = await SqlCommandHelperTx.ExecuteCommandTxAsync(
                    spParameters, tx.Connection, tx.Transaction,
                    command => new ValueTask<int>(command.ExecuteNonQueryAsync(cancellationToken)),
                    cancellationToken).ConfigureAwait(false);

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureNonQueryCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                        rowsAffected);

                return rowsAffected;
            }
            catch (CaeriusNetSqlException)
            {
                tx.Poison();
                throw;
            }
            finally
            {
                tx.ReleaseCommandSlot();
            }
        }

        /// <inheritdoc cref="WriteSqlAsyncCommands" />
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask ExecuteAsync(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
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
                var rowsAffected = await SqlCommandHelperTx.ExecuteCommandTxAsync(
                    spParameters, tx.Connection, tx.Transaction,
                    command => new ValueTask<int>(command.ExecuteNonQueryAsync(cancellationToken)),
                    cancellationToken).ConfigureAwait(false);

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureNonQueryCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                        rowsAffected);
            }
            catch (CaeriusNetSqlException)
            {
                tx.Poison();
                throw;
            }
            finally
            {
                tx.ReleaseCommandSlot();
            }
        }
    }
}