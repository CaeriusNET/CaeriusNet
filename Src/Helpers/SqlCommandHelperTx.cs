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
    internal static async ValueTask<TResultSet?> ScalarQueryTxAsync<TResultSet>(
        StoredProcedureParameters spParameters,
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        await using var command = SqlCommandHelper.BuildCommand(spParameters, connection, transaction);
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
        await using var command = SqlCommandHelper.BuildCommand(spParameters, connection, transaction);
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
        await using var command = SqlCommandHelper.BuildCommand(spParameters, connection, transaction);
        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);

        var results = await ResultSetMaterializer.ReadListAsync<TResultSet>(
            reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
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
        await using var command = SqlCommandHelper.BuildCommand(spParameters, connection, transaction);
        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);

        return await ResultSetMaterializer.ReadImmutableArrayAsync<TResultSet>(
            reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
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
            await using var command = SqlCommandHelper.BuildCommand(spParameters, connection, transaction);
            return await execute(command).ConfigureAwait(false);
        }
        catch (SqlException ex)
        {
            throw new CaeriusNetSqlException($"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
        }
    }
}
