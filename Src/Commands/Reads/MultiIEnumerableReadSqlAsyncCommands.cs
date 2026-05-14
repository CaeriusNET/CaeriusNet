namespace CaeriusNet.Commands.Reads;

/// <summary>
///     Execute stored procedures that return multiple result sets as enumerable sequences.
/// </summary>
public static class MultiIEnumerableReadSqlAsyncCommands
{
    extension(ICaeriusNetDbContext context)
    {
        /// <summary>
        ///     Execute a stored procedure and materialize up to two result sets as enumerable sequences.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A tuple containing the materialized result sets. Missing trailing result sets are empty.</returns>
        public async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>)>
            QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2>(StoredProcedureParameters spParameters,
                CancellationToken cancellationToken = default)
            where TResultSet1 : class, ISpMapper<TResultSet1>
            where TResultSet2 : class, ISpMapper<TResultSet2>
        {
            return await CaeriusActivityExtensions.InstrumentMultiResultSetAsync(context, spParameters, 2,
                nameof(QueryMultipleIEnumerableAsync), async command =>
                {
                    await using var reader = await command.ExecuteReaderAsync(
                        CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

                    var l1 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet1>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (l1, []);

                    var l2 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet2>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    return (l1, l2);
                }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Execute a stored procedure and materialize up to three result sets as enumerable sequences.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A tuple containing the materialized result sets. Missing trailing result sets are empty.</returns>
        public async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>)>
            QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3>(StoredProcedureParameters spParameters,
                CancellationToken cancellationToken = default)
            where TResultSet1 : class, ISpMapper<TResultSet1>
            where TResultSet2 : class, ISpMapper<TResultSet2>
            where TResultSet3 : class, ISpMapper<TResultSet3>
        {
            return await CaeriusActivityExtensions.InstrumentMultiResultSetAsync(context, spParameters, 3,
                nameof(QueryMultipleIEnumerableAsync), async command =>
                {
                    await using var reader = await command.ExecuteReaderAsync(
                        CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

                    var l1 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet1>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (l1, [], []);

                    var l2 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet2>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (l1, l2, []);

                    var l3 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet3>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    return (l1, l2, l3);
                }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Execute a stored procedure and materialize up to four result sets as enumerable sequences.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A tuple containing the materialized result sets. Missing trailing result sets are empty.</returns>
        public async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>,
                IEnumerable<TResultSet4>)>
            QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
                StoredProcedureParameters spParameters,
                CancellationToken cancellationToken = default)
            where TResultSet1 : class, ISpMapper<TResultSet1>
            where TResultSet2 : class, ISpMapper<TResultSet2>
            where TResultSet3 : class, ISpMapper<TResultSet3>
            where TResultSet4 : class, ISpMapper<TResultSet4>
        {
            return await CaeriusActivityExtensions.InstrumentMultiResultSetAsync(context, spParameters, 4,
                nameof(QueryMultipleIEnumerableAsync), async command =>
                {
                    await using var reader = await command.ExecuteReaderAsync(
                        CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

                    var l1 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet1>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (l1, [], [], []);

                    var l2 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet2>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (l1, l2, [], []);

                    var l3 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet3>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (l1, l2, l3, []);

                    var l4 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet4>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    return (l1, l2, l3, l4);
                }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Execute a stored procedure and materialize up to five result sets as enumerable sequences.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A tuple containing the materialized result sets. Missing trailing result sets are empty.</returns>
        public async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>,
                IEnumerable<TResultSet4>, IEnumerable<TResultSet5>)>
            QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>(
                StoredProcedureParameters spParameters,
                CancellationToken cancellationToken = default)
            where TResultSet1 : class, ISpMapper<TResultSet1>
            where TResultSet2 : class, ISpMapper<TResultSet2>
            where TResultSet3 : class, ISpMapper<TResultSet3>
            where TResultSet4 : class, ISpMapper<TResultSet4>
            where TResultSet5 : class, ISpMapper<TResultSet5>
        {
            return await CaeriusActivityExtensions.InstrumentMultiResultSetAsync(context, spParameters, 5,
                nameof(QueryMultipleIEnumerableAsync), async command =>
                {
                    await using var reader = await command.ExecuteReaderAsync(
                        CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

                    var l1 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet1>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (l1, [], [], [], []);

                    var l2 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet2>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (l1, l2, [], [], []);

                    var l3 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet3>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (l1, l2, l3, [], []);

                    var l4 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet4>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                        return (l1, l2, l3, l4, []);

                    var l5 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet5>(
                        reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

                    return (l1, l2, l3, l4, l5);
                }, cancellationToken).ConfigureAwait(false);
        }
    }
}
