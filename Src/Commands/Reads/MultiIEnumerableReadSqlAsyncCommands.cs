namespace CaeriusNet.Commands.Reads;

/// <summary>
///     Provides methods for asynchronously querying multiple result sets from a database and mapping them to IEnumerable
///     collections for efficient processing.
/// </summary>
public static class MultiIEnumerableReadSqlAsyncCommands
{
	/// <summary>
	///     Executes a stored procedure and returns multiple result sets as enumerable collections.
	/// </summary>
	public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>)>
		QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2>(
			this ICaeriusNetDbContext context,
			StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
	{
		return await SqlCommandUtility.ExecuteCommandAsync(context, spParameters, execute: async command => {
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
	///     Executes a stored procedure and returns three result sets as enumerable collections.
	/// </summary>
	public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>)>
		QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3>(
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
	///     Executes a stored procedure and returns multiple result sets as enumerable collections (4 sets).
	/// </summary>
	public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>,
			IEnumerable<TResultSet4>)>
		QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
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
	///     Executes a stored procedure and retrieves five result sets as enumerable collections.
	/// </summary>
	public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>,
			IEnumerable<TResultSet4>, IEnumerable<TResultSet5>)>
		QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>(
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