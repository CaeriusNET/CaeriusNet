namespace CaeriusNet.Helpers;

/// <summary>
///     Transactional execution overloads for <see cref="SqlCommandHelper" />. The connection is owned
///     by the caller (<c>ICaeriusNetTransaction</c>) and is therefore **never** opened or disposed here;
///     each <see cref="SqlCommand" /> is wired to the supplied <see cref="SqlTransaction" /> so the
///     server enlists every statement in the same scope.
/// </summary>
internal static class SqlCommandHelperTx
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static SqlCommand BuildCommand(
        StoredProcedureParameters spParameters,
        SqlConnection connection,
        SqlTransaction transaction)
    {
        var command = new SqlCommand($"{spParameters.SchemaName}.{spParameters.ProcedureName}", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = spParameters.CommandTimeout,
            Transaction = transaction
        };

        var paramsSpan = spParameters.GetParametersSpan();
        ref var searchSpace = ref MemoryMarshal.GetReference(paramsSpan);
        for (var i = 0; i < paramsSpan.Length; i++)
            command.Parameters.Add(Unsafe.Add(ref searchSpace, i));

        return command;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static async ValueTask<TResultSet?> ScalarQueryTxAsync<TResultSet>(
        StoredProcedureParameters spParameters,
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        await using var command = BuildCommand(spParameters, connection, transaction);
        await using var reader = await command
            .ExecuteReaderAsync(
                CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.SequentialAccess,
                cancellationToken)
            .ConfigureAwait(false);

        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? TResultSet.MapFromDataReader(reader)
            : null;
    }

    internal static async IAsyncEnumerable<TResultSet> StreamQueryTxAsync<TResultSet>(
        StoredProcedureParameters spParameters,
        SqlConnection connection,
        SqlTransaction transaction,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        await using var command = BuildCommand(spParameters, connection, transaction);
        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return TResultSet.MapFromDataReader(reader);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static async ValueTask<ReadOnlyCollection<TResultSet>> ResultSetAsReadOnlyCollectionTxAsync<TResultSet>(
        StoredProcedureParameters spParameters,
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        var results = new List<TResultSet>(spParameters.Capacity);

        await using var command = BuildCommand(spParameters, connection, transaction);
        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var item = TResultSet.MapFromDataReader(reader);
            CollectionsMarshal.SetCount(results, results.Count + 1);
            CollectionsMarshal.AsSpan(results)[^1] = item;
        }

        return results.AsReadOnly();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static async ValueTask<ImmutableArray<TResultSet>> ResultSetAsImmutableArrayTxAsync<TResultSet>(
        StoredProcedureParameters spParameters,
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        var buffer = ArrayPool<TResultSet>.Shared.Rent(spParameters.Capacity);
        var count = 0;

        try
        {
            await using var command = BuildCommand(spParameters, connection, transaction);
            await using var reader = await command
                .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
                .ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (count >= buffer.Length)
                {
                    var newBuffer = ArrayPool<TResultSet>.Shared.Rent(buffer.Length * 3 / 2);
                    buffer.AsSpan(0, count).CopyTo(newBuffer);
                    ArrayPool<TResultSet>.Shared.Return(buffer);
                    buffer = newBuffer;
                }

                buffer[count++] = TResultSet.MapFromDataReader(reader);
            }

            return [..buffer.AsSpan(0, count)];
        }
        finally
        {
            ArrayPool<TResultSet>.Shared.Return(buffer);
        }
    }

    /// <summary>
    ///     Transactional version of <c>ExecuteCommandAsync</c>. Wraps SQL exceptions and lets the caller
    ///     decide how to react (the public extension methods poison the transaction).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static async ValueTask<T> ExecuteCommandTxAsync<T>(
        StoredProcedureParameters spParameters,
        SqlConnection connection,
        SqlTransaction transaction,
        Func<SqlCommand, ValueTask<T>> execute,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = BuildCommand(spParameters, connection, transaction);
            return await execute(command).ConfigureAwait(false);
        }
        catch (SqlException ex)
        {
            throw new CaeriusNetSqlException($"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
        }
    }
}