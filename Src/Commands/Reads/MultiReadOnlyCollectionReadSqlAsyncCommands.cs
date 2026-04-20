namespace CaeriusNet.Commands.Reads;

/// <summary>
///     Execute stored procedures that return multiple result sets as read-only collections.
/// </summary>
public static class MultiReadOnlyCollectionReadSqlAsyncCommands
{
    extension(ICaeriusNetDbContext context)
    {
        /// <summary>
        ///     Execute a stored procedure and materialize up to two result sets as read-only collections.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A tuple containing the materialized result sets. Missing trailing result sets are empty.</returns>
        public async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>)>
            QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2>(StoredProcedureParameters spParameters,
                CancellationToken cancellationToken = default)
            where TResultSet1 : class, ISpMapper<TResultSet1>
            where TResultSet2 : class, ISpMapper<TResultSet2>
        {
            return await SqlCommandHelper.ExecuteCommandAsync(context, spParameters, async command =>
            {
                await using var reader = await command.ExecuteReaderAsync(
                    CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

                var l1 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet1>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r1 = l1.AsReadOnly();

                if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                    return (r1, []);

                var l2 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet2>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r2 = l2.AsReadOnly();

                return (r1, r2);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Execute a stored procedure and materialize up to three result sets as read-only collections.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A tuple containing the materialized result sets. Missing trailing result sets are empty.</returns>
        public async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>,
                ReadOnlyCollection<TResultSet3>)>
            QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2, TResultSet3>(
                StoredProcedureParameters spParameters,
                CancellationToken cancellationToken = default)
            where TResultSet1 : class, ISpMapper<TResultSet1>
            where TResultSet2 : class, ISpMapper<TResultSet2>
            where TResultSet3 : class, ISpMapper<TResultSet3>
        {
            return await SqlCommandHelper.ExecuteCommandAsync(context, spParameters, async command =>
            {
                await using var reader = await command.ExecuteReaderAsync(
                    CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

                var l1 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet1>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r1 = l1.AsReadOnly();

                if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                    return (r1, [], []);

                var l2 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet2>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r2 = l2.AsReadOnly();

                if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                    return (r1, r2, []);

                var l3 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet3>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r3 = l3.AsReadOnly();

                return (r1, r2, r3);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Execute a stored procedure and materialize up to four result sets as read-only collections.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A tuple containing the materialized result sets. Missing trailing result sets are empty.</returns>
        public async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>,
                ReadOnlyCollection<TResultSet3>, ReadOnlyCollection<TResultSet4>)>
            QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
                StoredProcedureParameters spParameters,
                CancellationToken cancellationToken = default)
            where TResultSet1 : class, ISpMapper<TResultSet1>
            where TResultSet2 : class, ISpMapper<TResultSet2>
            where TResultSet3 : class, ISpMapper<TResultSet3>
            where TResultSet4 : class, ISpMapper<TResultSet4>
        {
            return await SqlCommandHelper.ExecuteCommandAsync(context, spParameters, async command =>
            {
                await using var reader = await command.ExecuteReaderAsync(
                    CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

                var l1 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet1>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r1 = l1.AsReadOnly();

                if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                    return (r1, [], [], []);

                var l2 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet2>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r2 = l2.AsReadOnly();

                if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                    return (r1, r2, [], []);

                var l3 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet3>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r3 = l3.AsReadOnly();

                if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                    return (r1, r2, r3, []);

                var l4 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet4>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r4 = l4.AsReadOnly();

                return (r1, r2, r3, r4);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Execute a stored procedure and materialize up to five result sets as read-only collections.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A tuple containing the materialized result sets. Missing trailing result sets are empty.</returns>
        public async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>,
                ReadOnlyCollection<TResultSet3>, ReadOnlyCollection<TResultSet4>, ReadOnlyCollection<TResultSet5>)>
            QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>(
                StoredProcedureParameters spParameters,
                CancellationToken cancellationToken = default)
            where TResultSet1 : class, ISpMapper<TResultSet1>
            where TResultSet2 : class, ISpMapper<TResultSet2>
            where TResultSet3 : class, ISpMapper<TResultSet3>
            where TResultSet4 : class, ISpMapper<TResultSet4>
            where TResultSet5 : class, ISpMapper<TResultSet5>
        {
            return await SqlCommandHelper.ExecuteCommandAsync(context, spParameters, async command =>
            {
                await using var reader = await command.ExecuteReaderAsync(
                    CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

                var l1 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet1>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r1 = l1.AsReadOnly();

                if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                    return (r1, [], [], [], []);

                var l2 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet2>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r2 = l2.AsReadOnly();

                if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                    return (r1, r2, [], [], []);

                var l3 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet3>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r3 = l3.AsReadOnly();

                if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                    return (r1, r2, r3, [], []);

                var l4 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet4>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r4 = l4.AsReadOnly();

                if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
                    return (r1, r2, r3, r4, []);

                var l5 = await MultiResultSetHelper.ReadResultSetAsync<TResultSet5>(
                    reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);
                var r5 = l5.AsReadOnly();

                return (r1, r2, r3, r4, r5);
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}