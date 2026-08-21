using CaeriusNet.Execution;
using CaeriusNet.Mappers;
using CaeriusNet.Transactions;

namespace CaeriusNet;

public static class CaeriusNetCommandExtensions
{
    public static ValueTask<T> QueryFirstAsync<T>(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        return QueryCardinalityAsync<T>(context, command, true, false, cancellationToken)!;
    }

    public static ValueTask<T?> QueryFirstOrDefaultAsync<T>(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        return QueryCardinalityAsync<T>(context, command, false, false, cancellationToken);
    }

    public static ValueTask<T> QuerySingleAsync<T>(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        return QueryCardinalityAsync<T>(context, command, true, true, cancellationToken)!;
    }

    public static ValueTask<T?> QuerySingleOrDefaultAsync<T>(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        return QueryCardinalityAsync<T>(context, command, false, true, cancellationToken);
    }

    public static async ValueTask<T> ExecuteScalarAsync<T>(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        var execution = CaeriusExecutor.GetContext(context);
        var result = await CaeriusExecutor.RunAsync(
            execution,
            command,
            (sqlCommand, token) => CaeriusExecutor.ReadScalarAsync(sqlCommand, command, token),
            null,
            cancellationToken).ConfigureAwait(false);
        return CaeriusExecutor.ConvertRequiredScalar<T>(result, command);
    }

    public static async ValueTask<T?> ExecuteScalarOrDefaultAsync<T>(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        var execution = CaeriusExecutor.GetContext(context);
        var result = await CaeriusExecutor.RunAsync(
            execution,
            command,
            (sqlCommand, token) => CaeriusExecutor.ReadScalarAsync(sqlCommand, command, token),
            null,
            cancellationToken).ConfigureAwait(false);
        return CaeriusExecutor.ConvertOptionalScalar<T>(result, command);
    }

    public static async ValueTask<IEnumerable<T>> QueryAsync<T>(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        int? capacity = null,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        var execution = CaeriusExecutor.GetContext(context);
        var state = new QueryCacheState(command, capacity);
        var value = await CaeriusExecutor.CachedAsync<T[], QueryCacheState>(
            execution,
            command,
            $"enumerable:{typeof(T).FullName}",
            state,
            static (activeContext, cacheState, token) => ReadArrayAsync<T>(
                activeContext,
                cacheState.Command,
                cacheState.Capacity,
                null,
                token),
            cancellationToken).ConfigureAwait(false);
        return value;
    }

    public static async ValueTask<ReadOnlyCollection<T>> QueryReadOnlyCollectionAsync<T>(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        int? capacity = null,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        var execution = CaeriusExecutor.GetContext(context);
        return await CaeriusExecutor.CachedAsync<ReadOnlyCollection<T>, QueryCacheState>(
            execution,
            command,
            $"readonly:{typeof(T).FullName}",
            new QueryCacheState(command, capacity),
            static (activeContext, state, token) => ReadReadOnlyCollectionCachedAsync<T>(activeContext, state, token),
            cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<ImmutableArray<T>> QueryImmutableArrayAsync<T>(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        int? capacity = null,
        CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        var execution = CaeriusExecutor.GetContext(context);
        return await CaeriusExecutor.CachedAsync<ImmutableArray<T>, QueryCacheState>(
            execution,
            command,
            $"immutable:{typeof(T).FullName}",
            new QueryCacheState(command, capacity),
            static (activeContext, state, token) => ReadImmutableArrayCachedAsync<T>(activeContext, state, token),
            cancellationToken).ConfigureAwait(false);
    }

    public static async IAsyncEnumerable<T> StreamAsync<T>(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        CaeriusExecutor.EnsureStreamHasNoOutputs(command);
        cancellationToken.ThrowIfCancellationRequested();
        var execution = CaeriusExecutor.GetContext(context);
        ICaeriusSqlConnectionLease? lease = null;
        SqlCommand? sqlCommand = null;
        SqlDataReader? reader = null;
        CaeriusInstrumentation.Operation instrumentation = default;
        var succeeded = false;
        Exception? failure = null;
        long row = 0;
        try
        {
            try
            {
                lease = await execution.OpenLeaseAsync(cancellationToken).ConfigureAwait(false);
                sqlCommand = command.CreateSqlCommand(lease.Connection, cancellationToken: cancellationToken);
                instrumentation = execution.Instrumentation.Start(
                    command,
                    lease.Connection,
                    false,
                    execution.Options);
                reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken)
                    .ConfigureAwait(false);
                if (!await CaeriusExecutor.MoveToFirstColumnResultAsync(reader, cancellationToken)
                        .ConfigureAwait(false))
                {
                    await CaeriusExecutor.InvalidateAfterDurableSqlAsync(execution, command.InvalidationTags)
                        .ConfigureAwait(false);
                    instrumentation.Success();
                    succeeded = true;
                    yield break;
                }
            }
            catch (OperationCanceledException exception)
            {
                failure = exception;
                throw;
            }
            catch (SqlException exception)
            {
                CaeriusExecutor.RethrowCancellationIfRequested(cancellationToken);
                var wrapped = new CaeriusNetSqlException(command.FullName, exception);
                failure = wrapped;
                throw wrapped;
            }
            catch (Exception exception)
            {
                failure = exception;
                throw;
            }

            while (true)
            {
                bool hasRow;
                try
                {
                    hasRow = await ReadWrappedAsync(reader, command, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failure = exception;
                    throw;
                }

                if (!hasRow)
                    break;

                T mapped;
                try
                {
                    mapped = T.MapFromDataReader(reader);
                }
                catch (OperationCanceledException exception)
                {
                    failure = exception;
                    throw;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    var wrapped = new CaeriusMappingException(command.FullName, 0, row, exception);
                    failure = wrapped;
                    throw wrapped;
                }

                row++;
                yield return mapped;
            }

            bool hasAdditionalResultSet;
            try
            {
                hasAdditionalResultSet =
                    await NextResultWrappedAsync(reader, command, cancellationToken).ConfigureAwait(false);
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
                catch (SqlException exception)
                {
                    CaeriusExecutor.RethrowCancellationIfRequested(cancellationToken);
                    failure = new CaeriusNetSqlException(command.FullName, exception);
                    throw failure;
                }
                catch (Exception exception)
                {
                    failure = exception;
                    throw;
                }

                var contractException = new CaeriusContractException(
                    $"Stored procedure '{command.FullName}' returned additional result sets; StreamAsync accepts one.");
                failure = contractException;
                throw contractException;
            }

            await CaeriusExecutor.InvalidateAfterDurableSqlAsync(execution, command.InvalidationTags)
                .ConfigureAwait(false);
            instrumentation.Success();
            succeeded = true;
        }
        finally
        {
            if (!succeeded && failure is not null)
                instrumentation.Failure(failure);

            if (!succeeded)
                CancelWithoutMasking(sqlCommand);
            if (reader is not null)
                await DisposeWithoutMaskingAsync(reader, failure).ConfigureAwait(false);
            if (sqlCommand is not null)
                await DisposeWithoutMaskingAsync(sqlCommand, failure).ConfigureAwait(false);
            instrumentation.Dispose();
            if (lease is not null)
                await DisposeWithoutMaskingAsync(lease, failure).ConfigureAwait(false);
        }
    }

    public static async ValueTask<CaeriusExecutionResult<ImmutableArray<T>, CaeriusOutputValues>>
        QueryImmutableArrayWithOutputsAsync<T>(
            this ICaeriusNetDbContext context,
            StoredProcedureCommand command,
            int? capacity = null,
            CancellationToken cancellationToken = default)
        where T : class, ISpMapper<T>
    {
        var execution = CaeriusExecutor.GetContext(context);
        var result = await CaeriusExecutor.RunAsync(
            execution,
            command,
            async (sqlCommand, token) =>
            {
                var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default, token)
                    .ConfigureAwait(false);
                var items = new List<T>(ResolveCapacity(command, capacity, 0));
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
            null,
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    public static async ValueTask<int> ExecuteNonQueryAsync(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        var execution = CaeriusExecutor.GetContext(context);
        var affected = await CaeriusExecutor.RunAsync(
            execution,
            command,
            async (sqlCommand, token) => await sqlCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false),
            null,
            cancellationToken).ConfigureAwait(false);
        return affected;
    }

    public static async ValueTask ExecuteAsync(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        var execution = CaeriusExecutor.GetContext(context);
        await CaeriusExecutor.RunAsync(
            execution,
            command,
            async (sqlCommand, token) =>
            {
                await CaeriusExecutor.ExecuteNoResultAsync(sqlCommand, command, token).ConfigureAwait(false);
                return true;
            },
            null,
            cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<CaeriusCommandResult<CaeriusOutputValues>> ExecuteWithOutputsAsync(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        var execution = CaeriusExecutor.GetContext(context);
        var output = await CaeriusExecutor.RunAsync(
            execution,
            command,
            async (sqlCommand, token) =>
            {
                await CaeriusExecutor.ExecuteNoResultAsync(sqlCommand, command, token).ConfigureAwait(false);
                return CaeriusExecutor.ReadOutputs(sqlCommand);
            },
            null,
            cancellationToken).ConfigureAwait(false);
        return new CaeriusCommandResult<CaeriusOutputValues>(output);
    }

    public static async ValueTask<CaeriusExecutionResult<int, CaeriusOutputValues>> ExecuteNonQueryWithOutputsAsync(
        this ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        CancellationToken cancellationToken = default)
    {
        var execution = CaeriusExecutor.GetContext(context);
        var result = await CaeriusExecutor.RunAsync(
            execution,
            command,
            async (sqlCommand, token) =>
            {
                var affected = await sqlCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return new CaeriusExecutionResult<int, CaeriusOutputValues>(affected,
                    CaeriusExecutor.ReadOutputs(sqlCommand));
            },
            null,
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static async ValueTask<T?> QueryCardinalityAsync<T>(
        ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        bool requireRow,
        bool requireSingle,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        var execution = CaeriusExecutor.GetContext(context);
        return await CaeriusExecutor.RunAsync(
            execution,
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
                    if (requireRow)
                        throw new CaeriusContractException($"Stored procedure '{command.FullName}' returned no rows.");
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

                if (requireSingle && await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    await CaeriusExecutor.DrainRemainingAsync(reader, token).ConfigureAwait(false);
                    throw new CaeriusContractException(
                        $"Stored procedure '{command.FullName}' returned more than one row.");
                }

                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    // Intentionally drain the remaining rows after selecting the first value.
                }

                await CaeriusExecutor.EnsureNoAdditionalResultSetsAsync(reader, command, token).ConfigureAwait(false);
                return value;
            },
            null,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<List<T>> ReadListAsync<T>(
        ICaeriusExecutionContext execution,
        StoredProcedureCommand command,
        int? capacity,
        CaeriusNetTransaction? transaction,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        return await CaeriusExecutor.RunAsync(
            execution,
            command,
            async (sqlCommand, token) =>
            {
                await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default, token)
                    .ConfigureAwait(false);
                var result = new List<T>(ResolveCapacity(command, capacity, 0));
                long row = 0;
                var hasResultSet = await CaeriusExecutor.MoveToFirstColumnResultAsync(reader, token)
                    .ConfigureAwait(false);
                while (hasResultSet && await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    try
                    {
                        result.Add(T.MapFromDataReader(reader));
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        throw new CaeriusMappingException(command.FullName, 0, row, exception);
                    }

                    row++;
                }

                await CaeriusExecutor.EnsureNoAdditionalResultSetsAsync(reader, command, token).ConfigureAwait(false);
                return result;
            },
            transaction,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T[]> ReadArrayAsync<T>(
        ICaeriusExecutionContext execution,
        StoredProcedureCommand command,
        int? capacity,
        CaeriusNetTransaction? transaction,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        return (await ReadListAsync<T>(execution, command, capacity, transaction, cancellationToken)
            .ConfigureAwait(false)).ToArray();
    }

    internal static int ResolveCapacity(StoredProcedureCommand command, int? capacity, int resultSetIndex)
    {
        var requested = capacity ?? StoredProcedureCommand.UnspecifiedResultSetCapacity;
        var commandCapacity = command.ResultSetCapacities.Length > resultSetIndex
            ? command.ResultSetCapacities[resultSetIndex]
            : StoredProcedureCommand.UnspecifiedResultSetCapacity;
        var value = requested >= 0 ? requested : commandCapacity >= 0 ? commandCapacity : 16;
        if (requested < StoredProcedureCommand.UnspecifiedResultSetCapacity)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        return value;
    }

    private static async ValueTask<ReadOnlyCollection<T>> ReadReadOnlyCollectionCachedAsync<T>(
        ICaeriusExecutionContext execution,
        QueryCacheState state,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        return new ReadOnlyCollection<T>(await ReadListAsync<T>(
            execution,
            state.Command,
            state.Capacity,
            null,
            cancellationToken).ConfigureAwait(false));
    }

    private static async ValueTask<ImmutableArray<T>> ReadImmutableArrayCachedAsync<T>(
        ICaeriusExecutionContext execution,
        QueryCacheState state,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        return (await ReadListAsync<T>(
            execution,
            state.Command,
            state.Capacity,
            null,
            cancellationToken).ConfigureAwait(false)).ToImmutableArray();
    }

    private static async ValueTask<bool> ReadWrappedAsync(
        SqlDataReader reader,
        StoredProcedureCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException exception)
        {
            CaeriusExecutor.RethrowCancellationIfRequested(cancellationToken);
            throw new CaeriusNetSqlException(command.FullName, exception);
        }
    }

    private static async ValueTask<bool> NextResultWrappedAsync(
        SqlDataReader reader,
        StoredProcedureCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            // SQL Server can expose row-count/status frames when a procedure does not use
            // SET NOCOUNT ON.  They are not business result sets.  In particular, treating
            // one as an extra result set would make StreamAsync depend on NOCOUNT.
            while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
                if (reader.FieldCount > 0)
                    return true;
            return false;
        }
        catch (SqlException exception)
        {
            CaeriusExecutor.RethrowCancellationIfRequested(cancellationToken);
            throw new CaeriusNetSqlException(command.FullName, exception);
        }
    }

    private static async ValueTask DisposeWithoutMaskingAsync(IAsyncDisposable disposable, Exception? originalFailure)
    {
        try
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        catch (SqlException) when (originalFailure is not null)
        {
            // A provider cleanup failure must not replace the error which made streaming fail.
        }
        catch (ObjectDisposedException) when (originalFailure is not null)
        {
            // A redundant teardown must not replace the error which made streaming fail.
        }
        catch (InvalidOperationException) when (originalFailure is not null)
        {
            // A provider state transition during teardown must not replace the original failure.
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
        catch (SqlException)
        {
            // Cancellation during reader teardown is a best-effort protocol hint and must not hide the
            // primary mapping, cancellation, or contract failure.
        }
        catch (ObjectDisposedException)
        {
            // The command can already be disposed by another teardown path; preserve the primary failure.
        }
        catch (InvalidOperationException)
        {
            // A closed connection or completed command cannot be cancelled and must not mask the primary failure.
        }
    }

    private readonly record struct QueryCacheState(
        StoredProcedureCommand Command,
        int? Capacity);
}
