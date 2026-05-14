namespace CaeriusNet.Commands.Reads;

/// <summary>
///     Execute stored procedures that return multiple result sets as immutable arrays.
/// </summary>
public static class MultiImmutableArrayReadSqlAsyncCommands
{
    /// <param name="context">Database context used to open the connection.</param>
    extension(ICaeriusNetDbContext context)
    {
        /// <summary>
        ///     Execute a stored procedure and materialize up to two result sets as immutable arrays.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A tuple containing the materialized result sets. Missing trailing result sets are empty.</returns>
        public async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>)>
            QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2>(StoredProcedureParameters spParameters,
                CancellationToken cancellationToken = default)
            where TResultSet1 : class, ISpMapper<TResultSet1>
            where TResultSet2 : class, ISpMapper<TResultSet2>
        {
            return await CaeriusActivityExtensions.InstrumentMultiResultSetAsync(context, spParameters, 2,
                nameof(QueryMultipleImmutableArrayAsync), async command =>
                {
                    await using var reader = await command.ExecuteReaderAsync(
                        CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

                    var a1 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet1>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (a1, ImmutableArray<TResultSet2>.Empty);

                    var a2 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet2>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    return (a1, a2);
                }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Execute a stored procedure and materialize up to three result sets as immutable arrays.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A tuple containing the materialized result sets. Missing trailing result sets are empty.</returns>
        public async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>)>
            QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3>(
                StoredProcedureParameters spParameters,
                CancellationToken cancellationToken = default)
            where TResultSet1 : class, ISpMapper<TResultSet1>
            where TResultSet2 : class, ISpMapper<TResultSet2>
            where TResultSet3 : class, ISpMapper<TResultSet3>
        {
            return await CaeriusActivityExtensions.InstrumentMultiResultSetAsync(context, spParameters, 3,
                nameof(QueryMultipleImmutableArrayAsync), async command =>
                {
                    await using var reader = await command.ExecuteReaderAsync(
                        CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

                    var a1 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet1>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (a1, ImmutableArray<TResultSet2>.Empty, ImmutableArray<TResultSet3>.Empty);

                    var a2 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet2>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (a1, a2, ImmutableArray<TResultSet3>.Empty);

                    var a3 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet3>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    return (a1, a2, a3);
                }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Execute a stored procedure and materialize up to four result sets as immutable arrays.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A tuple containing the materialized result sets. Missing trailing result sets are empty.</returns>
        public async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>,
                ImmutableArray<TResultSet4>)>
            QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
                StoredProcedureParameters spParameters,
                CancellationToken cancellationToken = default)
            where TResultSet1 : class, ISpMapper<TResultSet1>
            where TResultSet2 : class, ISpMapper<TResultSet2>
            where TResultSet3 : class, ISpMapper<TResultSet3>
            where TResultSet4 : class, ISpMapper<TResultSet4>
        {
            return await CaeriusActivityExtensions.InstrumentMultiResultSetAsync(context, spParameters, 4,
                nameof(QueryMultipleImmutableArrayAsync), async command =>
                {
                    await using var reader = await command.ExecuteReaderAsync(
                        CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

                    var a1 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet1>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (a1, ImmutableArray<TResultSet2>.Empty, ImmutableArray<TResultSet3>.Empty,
                            ImmutableArray<TResultSet4>.Empty);

                    var a2 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet2>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (a1, a2, ImmutableArray<TResultSet3>.Empty, ImmutableArray<TResultSet4>.Empty);

                    var a3 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet3>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (a1, a2, a3, ImmutableArray<TResultSet4>.Empty);

                    var a4 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet4>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    return (a1, a2, a3, a4);
                }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Execute a stored procedure and materialize up to five result sets as immutable arrays.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A tuple containing the materialized result sets. Missing trailing result sets are empty.</returns>
        public async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>,
                ImmutableArray<TResultSet4>, ImmutableArray<TResultSet5>)>
            QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>(
                StoredProcedureParameters spParameters,
                CancellationToken cancellationToken = default)
            where TResultSet1 : class, ISpMapper<TResultSet1>
            where TResultSet2 : class, ISpMapper<TResultSet2>
            where TResultSet3 : class, ISpMapper<TResultSet3>
            where TResultSet4 : class, ISpMapper<TResultSet4>
            where TResultSet5 : class, ISpMapper<TResultSet5>
        {
            return await CaeriusActivityExtensions.InstrumentMultiResultSetAsync(context, spParameters, 5,
                nameof(QueryMultipleImmutableArrayAsync), async command =>
                {
                    await using var reader = await command.ExecuteReaderAsync(
                        CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

                    var a1 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet1>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (a1, ImmutableArray<TResultSet2>.Empty, ImmutableArray<TResultSet3>.Empty,
                            ImmutableArray<TResultSet4>.Empty, ImmutableArray<TResultSet5>.Empty);

                    var a2 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet2>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (a1, a2, ImmutableArray<TResultSet3>.Empty, ImmutableArray<TResultSet4>.Empty,
                            ImmutableArray<TResultSet5>.Empty);

                    var a3 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet3>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (a1, a2, a3, ImmutableArray<TResultSet4>.Empty, ImmutableArray<TResultSet5>.Empty);

                    var a4 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet4>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (a1, a2, a3, a4, ImmutableArray<TResultSet5>.Empty);

                    var a5 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet5>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    return (a1, a2, a3, a4, a5);
                }, cancellationToken).ConfigureAwait(false);
        }
    }
}
