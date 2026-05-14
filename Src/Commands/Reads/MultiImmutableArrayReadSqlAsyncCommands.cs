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
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public ValueTask<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>)>
            QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2>(StoredProcedureParameters spParameters,
                CancellationToken cancellationToken = default)
            where TResultSet1 : class, ISpMapper<TResultSet1>
            where TResultSet2 : class, ISpMapper<TResultSet2>
        {
            return CaeriusActivityExtensions
                .InstrumentMultiResultSetAsync<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>)>(
                    context, spParameters, 2,
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
                }, cancellationToken);
        }
    }
}
