namespace CaeriusNet.Commands.Reads;

/// <summary>
///     Provides asynchronous methods for executing database queries that return multiple result sets,
///     each mapped to an ImmutableArray of strongly typed objects.
/// </summary>
public static class MultiImmutableArrayReadSqlAsyncCommands
{
	/// <summary>
	///     Executes a stored procedure and maps the results into two immutable arrays of specified result types.
	/// </summary>
	/// <typeparam name="TResultSet1">The type of the first result set.</typeparam>
	/// <typeparam name="TResultSet2">The type of the second result set.</typeparam>
	/// <param name="context">The database context for executing the query.</param>
	/// <param name="spParameters">The parameters required to execute the stored procedure.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A tuple containing two immutable arrays, one for each result set.</returns>
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2>(
			this ICaeriusNetDbContext context,
			StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
	{
		return await SqlCommandUtility.ExecuteCommandAsync(context, spParameters, execute: async command => {
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
	///     Executes a stored procedure and maps the results into three immutable arrays.
	/// </summary>
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3>(
			this ICaeriusNetDbContext context,
			StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
	{
		return await SqlCommandUtility.ExecuteCommandAsync(context, spParameters, execute: async command => {
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
	///     Executes a stored procedure and maps the results into four immutable arrays.
	/// </summary>
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>,
			ImmutableArray<TResultSet4>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
			this ICaeriusNetDbContext context,
			StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
		where TResultSet4 : class, ISpMapper<TResultSet4>
	{
		return await SqlCommandUtility.ExecuteCommandAsync(context, spParameters, execute: async command => {
			await using var reader = await command.ExecuteReaderAsync(
			CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

			var a1 = await MultiResultSetHelper.ReadResultSetAsImmutableArrayAsync<TResultSet1>(
			reader, spParameters.Capacity, cancellationToken).ConfigureAwait(false);

			if (!await MultiResultSetHelper.TryMoveNextAsync(reader, cancellationToken).ConfigureAwait(false))
				return (a1, ImmutableArray<TResultSet2>.Empty, ImmutableArray<TResultSet3>.Empty, ImmutableArray<TResultSet4>.Empty);

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
	///     Executes a stored procedure and maps the results into five immutable arrays.
	/// </summary>
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>,
			ImmutableArray<TResultSet4>, ImmutableArray<TResultSet5>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>(
			this ICaeriusNetDbContext context,
			StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
		where TResultSet4 : class, ISpMapper<TResultSet4>
		where TResultSet5 : class, ISpMapper<TResultSet5>
	{
		return await SqlCommandUtility.ExecuteCommandAsync(context, spParameters, execute: async command => {
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