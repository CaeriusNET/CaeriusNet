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
			await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

			var l1 = new List<TResultSet1>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l1.Add(TResultSet1.MapFromDataReader(reader));

			List<TResultSet2> l2 = [];
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (l1, l2);

			l2 = new List<TResultSet2>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l2.Add(TResultSet2.MapFromDataReader(reader));

			// Return lists directly as IEnumerable<T> (no extra iterator allocation).
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
			await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

			var l1 = new List<TResultSet1>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l1.Add(TResultSet1.MapFromDataReader(reader));

			List<TResultSet2> l2 = [];
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (l1, l2, new List<TResultSet3>(0));

			l2 = new List<TResultSet2>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l2.Add(TResultSet2.MapFromDataReader(reader));

			List<TResultSet3> l3 = [];
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (l1, l2, l3);

			l3 = new List<TResultSet3>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l3.Add(TResultSet3.MapFromDataReader(reader));

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
			await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

			var l1 = new List<TResultSet1>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l1.Add(TResultSet1.MapFromDataReader(reader));

			List<TResultSet2> l2 = [];
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (l1, l2, new List<TResultSet3>(0), new List<TResultSet4>(0));

			l2 = new List<TResultSet2>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l2.Add(TResultSet2.MapFromDataReader(reader));

			List<TResultSet3> l3 = [];
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (l1, l2, l3, []);

			l3 = new List<TResultSet3>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l3.Add(TResultSet3.MapFromDataReader(reader));

			List<TResultSet4> l4 = [];
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (l1, l2, l3, l4);

			l4 = new List<TResultSet4>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l4.Add(TResultSet4.MapFromDataReader(reader));

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
			await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

			var l1 = new List<TResultSet1>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l1.Add(TResultSet1.MapFromDataReader(reader));

			List<TResultSet2> l2 = [];
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (l1, l2, new List<TResultSet3>(0), new List<TResultSet4>(0), new List<TResultSet5>(0));

			l2 = new List<TResultSet2>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l2.Add(TResultSet2.MapFromDataReader(reader));

			List<TResultSet3> l3 = [];
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (l1, l2, l3, [], []);

			l3 = new List<TResultSet3>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l3.Add(TResultSet3.MapFromDataReader(reader));

			List<TResultSet4> l4 = [];
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (l1, l2, l3, l4, []);

			l4 = new List<TResultSet4>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l4.Add(TResultSet4.MapFromDataReader(reader));

			List<TResultSet5> l5 = [];
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (l1, l2, l3, l4, l5);

			l5 = new List<TResultSet5>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l5.Add(TResultSet5.MapFromDataReader(reader));

			return (l1, l2, l3, l4, l5);
		}, cancellationToken).ConfigureAwait(false);
	}
}