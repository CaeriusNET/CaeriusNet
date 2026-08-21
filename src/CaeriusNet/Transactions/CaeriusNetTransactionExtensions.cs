using CaeriusNet.Execution;
using CaeriusNet.Mappers;
using CaeriusNet.Transactions;

namespace CaeriusNet;

public static class CaeriusNetTransactionExtensions
{
    public static ValueTask<ICaeriusNetTransaction> BeginTransactionAsync(
        this ICaeriusNetDbContext context,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        return BeginCoreAsync(CaeriusExecutor.GetContext(context), isolationLevel, cancellationToken);
    }

    public static async ValueTask<IEnumerable<T>> QueryAsync<T>(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        int? capacity = null,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        var tx = GetTransaction(transaction);
        return (await CaeriusNetCommandExtensions.ReadListAsync<T>(
            tx.Context, command, capacity, tx, cancellationToken).ConfigureAwait(false)).ToArray();
    }

    public static async ValueTask<ReadOnlyCollection<T>> QueryReadOnlyCollectionAsync<T>(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        int? capacity = null,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        return new ReadOnlyCollection<T>(await CaeriusNetCommandExtensions.ReadListAsync<T>(
            GetTransaction(transaction).Context,
            command,
            capacity,
            GetTransaction(transaction),
            cancellationToken).ConfigureAwait(false));
    }

    public static async ValueTask<ImmutableArray<T>> QueryImmutableArrayAsync<T>(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        int? capacity = null,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        return (await CaeriusNetCommandExtensions.ReadListAsync<T>(
            GetTransaction(transaction).Context,
            command,
            capacity,
            GetTransaction(transaction),
            cancellationToken).ConfigureAwait(false)).ToImmutableArray();
    }

    /// <summary>
    ///     Streams exactly one result set while retaining the transaction's connection and command slot for the
    ///     lifetime of the enumeration. Dispose the enumerator (normally with <c>await foreach</c>) before
    ///     committing or rolling back the transaction.
    /// </summary>
    /// <remarks>
    ///     Streaming intentionally bypasses read-through caches. Declared invalidations are deferred as soon as
    ///     SQL Server has accepted the command; a later command failure poisons the transaction and rollback
    ///     discards them. Stopping enumeration before the single result set is fully consumed poisons the
    ///     transaction, so it must be rolled back rather than committed.
    /// </remarks>
    public static async IAsyncEnumerable<T> StreamAsync<T>(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        ArgumentNullException.ThrowIfNull(command);
        CaeriusExecutor.EnsureStreamHasNoOutputs(command);
        cancellationToken.ThrowIfCancellationRequested();
        var tx = GetTransaction(transaction);
        SqlCommand? sqlCommand = null;
        SqlDataReader? reader = null;
        CaeriusInstrumentation.Operation instrumentation = default;
        Exception? failure = null;
        var succeeded = false;
        long row = 0;

        tx.AcquireCommandSlot();
        try
        {
            sqlCommand = command.CreateSqlCommand(tx.Connection, tx.Transaction, cancellationToken);
            instrumentation = tx.Context.Instrumentation.Start(command, tx.Connection, true, tx.Context.Options);
            reader = await ExecuteTransactionReaderAsync(sqlCommand, command, tx, cancellationToken)
                .ConfigureAwait(false);

            // Unlike materialized operations this command remains live while the caller enumerates. Register
            // invalidations now, while the slot is owned; rollback clears them if the stream subsequently fails.
            tx.DeferInvalidations(command.InvalidationTags);

            if (!await CaeriusExecutor.MoveToFirstColumnResultAsync(reader, cancellationToken)
                    .ConfigureAwait(false))
            {
                instrumentation.Success();
                succeeded = true;
                yield break;
            }

            while (true)
            {
                bool hasRow;
                try
                {
                    hasRow = await ReadTransactionStreamAsync(reader, command, tx, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failure = exception;
                    throw;
                }

                if (!hasRow)
                    break;

                T value;
                try
                {
                    value = T.MapFromDataReader(reader);
                }
                catch (OperationCanceledException exception)
                {
                    failure = exception;
                    throw;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failure = new CaeriusMappingException(command.FullName, 0, row, exception);
                    tx.Poison();
                    throw failure;
                }

                row++;
                yield return value;
            }

            bool hasAdditionalResultSet;
            try
            {
                hasAdditionalResultSet = await NextTransactionStreamAsync(reader, command, tx, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = exception;
                throw;
            }

            if (hasAdditionalResultSet)
            {
                try
                {
                    await CaeriusExecutor.DrainRemainingAsync(reader, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception)
                {
                    failure = exception;
                    throw;
                }
                catch (SqlException exception)
                {
                    CaeriusExecutor.RethrowCancellationIfRequested(cancellationToken, tx);
                    failure = new CaeriusNetSqlException(command.FullName, exception);
                    tx.Poison();
                    throw failure;
                }

                failure = new CaeriusContractException(
                    $"Stored procedure '{command.FullName}' returned additional result sets; StreamAsync accepts one.");
                tx.Poison();
                throw failure;
            }

            instrumentation.Success();
            succeeded = true;
        }
        finally
        {
            if (!succeeded && failure is null)
                failure = new CaeriusContractException(
                    $"Streaming stored procedure '{command.FullName}' was disposed before its result set completed.");
            // A partially consumed reader leaves the connection's protocol state indeterminate.  This includes
            // cancellation: cancellation is still surfaced unchanged as OperationCanceledException, but the
            // owning transaction must subsequently be rolled back instead of accepting another command/commit.
            if (!succeeded)
                tx.Poison();
            try
            {
                if (!succeeded && failure is not null)
                    instrumentation.Failure(failure);
                if (!succeeded)
                    CancelWithoutMasking(sqlCommand);
                if (reader is not null)
                    await DisposeWithoutMaskingAsync(reader, failure).ConfigureAwait(false);
                if (sqlCommand is not null)
                    await DisposeWithoutMaskingAsync(sqlCommand, failure).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    instrumentation.Dispose();
                }
                finally
                {
                    tx.ReleaseCommandSlot();
                }
            }
        }
    }

    /// <summary>
    ///     Executes a single-result stored procedure inside <paramref name="transaction" /> and returns its
    ///     fully materialized enumerable result together with SQL output and return-value parameters.
    /// </summary>
    /// <remarks>
    ///     The reader is completely consumed and disposed before output parameters are observed. Any declared
    ///     cache invalidations are deferred until <see cref="ICaeriusNetTransaction.CommitAsync" /> succeeds.
    /// </remarks>
    public static async ValueTask<CaeriusExecutionResult<IEnumerable<T>, CaeriusOutputValues>>
        QueryWithOutputsAsync<T>(
            this ICaeriusNetTransaction transaction,
            StoredProcedureCommand command,
            int? capacity = null,
            CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        var result = await ReadImmutableArrayWithOutputsAsync<T>(transaction, command, capacity, cancellationToken)
            .ConfigureAwait(false);
        return new CaeriusExecutionResult<IEnumerable<T>, CaeriusOutputValues>(result.Data, result.Output);
    }

    /// <summary>
    ///     Executes a single-result stored procedure inside <paramref name="transaction" /> and returns a
    ///     read-only materialized result together with SQL output and return-value parameters.
    /// </summary>
    public static async ValueTask<CaeriusExecutionResult<ReadOnlyCollection<T>, CaeriusOutputValues>>
        QueryReadOnlyCollectionWithOutputsAsync<T>(
            this ICaeriusNetTransaction transaction,
            StoredProcedureCommand command,
            int? capacity = null,
            CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        var result = await ReadImmutableArrayWithOutputsAsync<T>(transaction, command, capacity, cancellationToken)
            .ConfigureAwait(false);
        return new CaeriusExecutionResult<ReadOnlyCollection<T>, CaeriusOutputValues>(
            new ReadOnlyCollection<T>(result.Data.ToArray()),
            result.Output);
    }

    /// <summary>
    ///     Executes a single-result stored procedure inside <paramref name="transaction" /> and returns an
    ///     immutable result together with SQL output and return-value parameters.
    /// </summary>
    public static ValueTask<CaeriusExecutionResult<ImmutableArray<T>, CaeriusOutputValues>>
        QueryImmutableArrayWithOutputsAsync<T>(
            this ICaeriusNetTransaction transaction,
            StoredProcedureCommand command,
            int? capacity = null,
            CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        return ReadImmutableArrayWithOutputsAsync<T>(transaction, command, capacity, cancellationToken);
    }

    public static async ValueTask<T?> QueryFirstOrDefaultAsync<T>(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        var tx = GetTransaction(transaction);
        return await CaeriusExecutor.RunAsync(
            tx.Context,
            command,
            async (sqlCommand, token) =>
            {
                await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default, token)
                    .ConfigureAwait(false);
                var hasResultSet = await CaeriusExecutor.MoveToFirstColumnResultAsync(reader, token)
                    .ConfigureAwait(false);
                if (!hasResultSet || !await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    await CaeriusExecutor.EnsureNoAdditionalResultSetsAsync(reader, command, token)
                        .ConfigureAwait(false);
                    return null;
                }

                T value;
                try
                {
                    value = T.MapFromDataReader(reader);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    throw new CaeriusMappingException(command.FullName, 0, 0, exception);
                }

                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    // Intentionally drain the remaining rows after selecting the first value.
                }

                await CaeriusExecutor.EnsureNoAdditionalResultSetsAsync(reader, command, token).ConfigureAwait(false);
                return value;
            },
            tx,
            cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<T> QueryFirstAsync<T>(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        return await transaction.QueryFirstOrDefaultAsync<T>(command, cancellationToken).ConfigureAwait(false)
               ?? throw new CaeriusContractException($"Stored procedure '{command.FullName}' returned no rows.");
    }

    public static async ValueTask<T?> QuerySingleOrDefaultAsync<T>(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        var tx = GetTransaction(transaction);
        return await CaeriusExecutor.RunAsync(
            tx.Context,
            command,
            async (sqlCommand, token) =>
            {
                await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default, token)
                    .ConfigureAwait(false);
                var hasResultSet = await CaeriusExecutor.MoveToFirstColumnResultAsync(reader, token)
                    .ConfigureAwait(false);
                if (!hasResultSet || !await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    await CaeriusExecutor.EnsureNoAdditionalResultSetsAsync(reader, command, token)
                        .ConfigureAwait(false);
                    return null;
                }

                T value;
                try
                {
                    value = T.MapFromDataReader(reader);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    throw new CaeriusMappingException(command.FullName, 0, 0, exception);
                }

                if (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    await CaeriusExecutor.DrainRemainingAsync(reader, token).ConfigureAwait(false);
                    throw new CaeriusContractException(
                        $"Stored procedure '{command.FullName}' returned more than one row.");
                }

                await CaeriusExecutor.EnsureNoAdditionalResultSetsAsync(reader, command, token).ConfigureAwait(false);
                return value;
            },
            tx,
            cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<T> QuerySingleAsync<T>(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        return await transaction.QuerySingleOrDefaultAsync<T>(command, cancellationToken).ConfigureAwait(false)
               ?? throw new CaeriusContractException($"Stored procedure '{command.FullName}' returned no rows.");
    }

    public static async ValueTask<T> ExecuteScalarAsync<T>(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        var row = await ExecuteScalarCoreAsync(transaction, command, cancellationToken).ConfigureAwait(false);
        return CaeriusExecutor.ConvertRequiredScalar<T>(row, command);
    }

    public static async ValueTask<T?> ExecuteScalarOrDefaultAsync<T>(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        var row = await ExecuteScalarCoreAsync(transaction, command, cancellationToken).ConfigureAwait(false);
        return CaeriusExecutor.ConvertOptionalScalar<T>(row, command);
    }

    public static async ValueTask<int> ExecuteNonQueryAsync(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        var tx = GetTransaction(transaction);
        var affected = await CaeriusExecutor.RunAsync(
            tx.Context,
            command,
            async (sqlCommand, token) => { return await sqlCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false); },
            tx,
            cancellationToken).ConfigureAwait(false);
        return affected;
    }

    public static async ValueTask ExecuteAsync(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        var tx = GetTransaction(transaction);
        await CaeriusExecutor.RunAsync(
            tx.Context,
            command,
            async (sqlCommand, token) =>
            {
                await CaeriusExecutor.ExecuteNoResultAsync(sqlCommand, command, token).ConfigureAwait(false);
                return true;
            },
            tx,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Executes a no-data command inside <paramref name="transaction" /> and returns SQL output and
    ///     return-value parameters. Declared invalidations remain deferred until commit.
    /// </summary>
    public static async ValueTask<CaeriusCommandResult<CaeriusOutputValues>> ExecuteWithOutputsAsync(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        var tx = GetTransaction(transaction);
        var output = await CaeriusExecutor.RunAsync(
            tx.Context,
            command,
            async (sqlCommand, token) =>
            {
                await CaeriusExecutor.ExecuteNoResultAsync(sqlCommand, command, token).ConfigureAwait(false);
                return CaeriusExecutor.ReadOutputs(sqlCommand);
            },
            tx,
            cancellationToken).ConfigureAwait(false);
        return new CaeriusCommandResult<CaeriusOutputValues>(output);
    }

    /// <summary>
    ///     Executes a command inside <paramref name="transaction" /> and returns its affected-row count plus
    ///     SQL output and return-value parameters. Declared invalidations remain deferred until commit.
    /// </summary>
    public static async ValueTask<CaeriusExecutionResult<int, CaeriusOutputValues>> ExecuteNonQueryWithOutputsAsync(
        this ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        var tx = GetTransaction(transaction);
        return await CaeriusExecutor.RunAsync(
            tx.Context,
            command,
            async (sqlCommand, token) =>
            {
                var affected = await sqlCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return new CaeriusExecutionResult<int, CaeriusOutputValues>(
                    affected,
                    CaeriusExecutor.ReadOutputs(sqlCommand));
            },
            tx,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<ICaeriusNetTransaction> BeginCoreAsync(
        ICaeriusExecutionContext context,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        return await CaeriusNetTransaction.BeginAsync(context, isolationLevel, cancellationToken).ConfigureAwait(false);
    }

    private static CaeriusNetTransaction GetTransaction(ICaeriusNetTransaction transaction)
    {
        return transaction as CaeriusNetTransaction
               ?? throw new ArgumentException("The transaction was not created by CaeriusNet.", nameof(transaction));
    }

    private static async ValueTask<CaeriusScalarRow> ExecuteScalarCoreAsync(
        ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        CancellationToken cancellationToken)
    {
        var tx = GetTransaction(transaction);
        return await CaeriusExecutor.RunAsync(
            tx.Context,
            command,
            (sqlCommand, token) => CaeriusExecutor.ReadScalarAsync(sqlCommand, command, token),
            tx,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<CaeriusExecutionResult<ImmutableArray<T>, CaeriusOutputValues>>
        ReadImmutableArrayWithOutputsAsync<T>(
            ICaeriusNetTransaction transaction,
            StoredProcedureCommand command,
            int? capacity,
            CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        var tx = GetTransaction(transaction);
        return await CaeriusExecutor.RunAsync(
            tx.Context,
            command,
            async (sqlCommand, token) =>
            {
                var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default, token)
                    .ConfigureAwait(false);
                var items = new List<T>(CaeriusNetCommandExtensions.ResolveCapacity(command, capacity, 0));
                long row = 0;
                try
                {
                    var hasResultSet = await CaeriusExecutor.MoveToFirstColumnResultAsync(reader, token)
                        .ConfigureAwait(false);
                    while (hasResultSet && await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        try
                        {
                            items.Add(T.MapFromDataReader(reader));
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            throw new CaeriusMappingException(command.FullName, 0, row, exception);
                        }

                        row++;
                    }

                    await CaeriusExecutor.EnsureNoAdditionalResultSetsAsync(reader, command, token)
                        .ConfigureAwait(false);
                }
                finally
                {
                    await reader.DisposeAsync().ConfigureAwait(false);
                }

                return new CaeriusExecutionResult<ImmutableArray<T>, CaeriusOutputValues>(
                    [.. items],
                    CaeriusExecutor.ReadOutputs(sqlCommand));
            },
            tx,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask DisposeWithoutMaskingAsync(IAsyncDisposable disposable, Exception? originalFailure)
    {
        try
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        catch when (originalFailure is not null)
        {
            // Preserve the failure which made the command unusable over provider cleanup failures.
        }
    }

    private static void CancelWithoutMasking(SqlCommand? sqlCommand)
    {
        if (sqlCommand is null)
            return;
        try
        {
            sqlCommand.Cancel();
        }
        catch
        {
            // The command may already have completed or failed. Preserve the caller-visible failure while
            // still giving SqlClient a chance to stop sending a partially consumed result stream.
        }
    }

    private static async ValueTask<SqlDataReader> ExecuteTransactionReaderAsync(
        SqlCommand sqlCommand,
        StoredProcedureCommand command,
        CaeriusNetTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            return await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            transaction.Poison();
            throw;
        }
        catch (SqlException exception)
        {
            CaeriusExecutor.RethrowCancellationIfRequested(cancellationToken, transaction);
            transaction.Poison();
            throw new CaeriusNetSqlException(command.FullName, exception);
        }
        catch
        {
            transaction.Poison();
            throw;
        }
    }

    private static async ValueTask<bool> ReadTransactionStreamAsync(
        SqlDataReader reader,
        StoredProcedureCommand command,
        CaeriusNetTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            transaction.Poison();
            throw;
        }
        catch (SqlException exception)
        {
            CaeriusExecutor.RethrowCancellationIfRequested(cancellationToken, transaction);
            transaction.Poison();
            throw new CaeriusNetSqlException(command.FullName, exception);
        }
        catch
        {
            transaction.Poison();
            throw;
        }
    }

    private static async ValueTask<bool> NextTransactionStreamAsync(
        SqlDataReader reader,
        StoredProcedureCommand command,
        CaeriusNetTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            // Keep the transactional streaming contract identical to the regular one:
            // command-completion frames have FieldCount == 0 and must not be interpreted as
            // an additional business result set.
            while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
                if (reader.FieldCount > 0)
                    return true;
            return false;
        }
        catch (OperationCanceledException)
        {
            transaction.Poison();
            throw;
        }
        catch (SqlException exception)
        {
            CaeriusExecutor.RethrowCancellationIfRequested(cancellationToken, transaction);
            transaction.Poison();
            throw new CaeriusNetSqlException(command.FullName, exception);
        }
        catch
        {
            transaction.Poison();
            throw;
        }
    }
}
