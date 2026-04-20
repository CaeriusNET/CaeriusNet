namespace CaeriusNet.Commands.Transactions;

/// <summary>
///     Asynchronous write commands scoped to an <see cref="ICaeriusNetTransaction" />. They share the
///     transaction's connection and attach the underlying <see cref="SqlTransaction" /> to every command.
/// </summary>
public static class TransactionWriteSqlAsyncCommands
{
    /// <param name="transaction">The transaction whose connection / scope is reused.</param>
    extension(ICaeriusNetTransaction transaction)
    {
        /// <inheritdoc cref="WriteSqlAsyncCommands" />
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<T?> ExecuteScalarAsync<T>(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
        {
            var tx = AsInternal(transaction);
            tx.AcquireCommandSlot();
            try
            {
                return await SqlCommandHelperTx.ExecuteCommandTxAsync(
                    spParameters, tx.Connection, tx.Transaction,
                    async command =>
                    {
                        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                        return result is DBNull ? default : (T?)result;
                    }, cancellationToken).ConfigureAwait(false);
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
            tx.AcquireCommandSlot();
            try
            {
                return await SqlCommandHelperTx.ExecuteCommandTxAsync(
                    spParameters, tx.Connection, tx.Transaction,
                    command => new ValueTask<int>(command.ExecuteNonQueryAsync(cancellationToken)),
                    cancellationToken).ConfigureAwait(false);
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
            tx.AcquireCommandSlot();
            try
            {
                await SqlCommandHelperTx.ExecuteCommandTxAsync<object?>(
                    spParameters, tx.Connection, tx.Transaction,
                    async command =>
                    {
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        return null;
                    }, cancellationToken).ConfigureAwait(false);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ICaeriusNetTransactionInternal AsInternal(ICaeriusNetTransaction transaction)
    {
        return transaction as ICaeriusNetTransactionInternal
               ?? throw new InvalidOperationException(
                   "ICaeriusNetTransaction implementations must derive from the framework's CaeriusNetTransaction.");
    }
}
