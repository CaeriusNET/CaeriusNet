namespace CaeriusNet.Commands.Reads;

public static class MultiReadOnlyCollectionReadSqlAsyncCommands
{
	/// <summary>
	///     Executes a stored procedure to retrieve two result sets, mapping each into a read-only collection of the specified
	///     types, with zero delegate indirections and minimal allocations.
	/// </summary>
	public static async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>)>
		QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2>(
			this ICaeriusNetDbContext netDbContext, StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
	{
		return await SqlCommandUtility.ExecuteCommandAsync(netDbContext, spParameters, execute: async command => {
			await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

			var l1 = new List<TResultSet1>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l1.Add(TResultSet1.MapFromDataReader(reader));
			var r1 = l1.AsReadOnly();

			var r2 = new List<TResultSet2>(0).AsReadOnly();
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (r1, r2);

			var l2 = new List<TResultSet2>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l2.Add(TResultSet2.MapFromDataReader(reader));
			r2 = l2.AsReadOnly();

			return (r1, r2);
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	///     Executes a stored procedure to retrieve three result sets, mapping each into a read-only collection.
	/// </summary>
	public static async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>,
			ReadOnlyCollection<TResultSet3>)>
		QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2, TResultSet3>(
			this ICaeriusNetDbContext netDbContext, StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
	{
		return await SqlCommandUtility.ExecuteCommandAsync(netDbContext, spParameters, execute: async command => {
			await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

			var l1 = new List<TResultSet1>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l1.Add(TResultSet1.MapFromDataReader(reader));
			var r1 = l1.AsReadOnly();

			var r2 = new List<TResultSet2>(0).AsReadOnly();
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (r1, r2, new List<TResultSet3>(0).AsReadOnly());

			var l2 = new List<TResultSet2>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l2.Add(TResultSet2.MapFromDataReader(reader));
			r2 = l2.AsReadOnly();

			var r3 = new List<TResultSet3>(0).AsReadOnly();
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (r1, r2, r3);

			var l3 = new List<TResultSet3>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l3.Add(TResultSet3.MapFromDataReader(reader));
			r3 = l3.AsReadOnly();

			return (r1, r2, r3);
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	///     Executes a stored procedure to retrieve four result sets, mapping each into a read-only collection.
	/// </summary>
	public static async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>,
			ReadOnlyCollection<TResultSet3>, ReadOnlyCollection<TResultSet4>)>
		QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
			this ICaeriusNetDbContext netDbContext, StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
		where TResultSet4 : class, ISpMapper<TResultSet4>
	{
		return await SqlCommandUtility.ExecuteCommandAsync(netDbContext, spParameters, execute: async command => {
			await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

			var l1 = new List<TResultSet1>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l1.Add(TResultSet1.MapFromDataReader(reader));
			var r1 = l1.AsReadOnly();

			var r2 = new List<TResultSet2>(0).AsReadOnly();
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (r1, r2, new List<TResultSet3>(0).AsReadOnly(), new List<TResultSet4>(0).AsReadOnly());

			var l2 = new List<TResultSet2>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l2.Add(TResultSet2.MapFromDataReader(reader));
			r2 = l2.AsReadOnly();

			var r3 = new List<TResultSet3>(0).AsReadOnly();
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (r1, r2, r3, new List<TResultSet4>(0).AsReadOnly());

			var l3 = new List<TResultSet3>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l3.Add(TResultSet3.MapFromDataReader(reader));
			r3 = l3.AsReadOnly();

			var r4 = new List<TResultSet4>(0).AsReadOnly();
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (r1, r2, r3, r4);

			var l4 = new List<TResultSet4>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l4.Add(TResultSet4.MapFromDataReader(reader));
			r4 = l4.AsReadOnly();

			return (r1, r2, r3, r4);
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	///     Executes a stored procedure to retrieve five result sets, mapping each into a read-only collection.
	/// </summary>
	public static async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>,
			ReadOnlyCollection<TResultSet3>, ReadOnlyCollection<TResultSet4>, ReadOnlyCollection<TResultSet5>)>
		QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>(
			this ICaeriusNetDbContext netDbContext, StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
		where TResultSet4 : class, ISpMapper<TResultSet4>
		where TResultSet5 : class, ISpMapper<TResultSet5>
	{
		return await SqlCommandUtility.ExecuteCommandAsync(netDbContext, spParameters, execute: async command => {
			await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

			var l1 = new List<TResultSet1>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l1.Add(TResultSet1.MapFromDataReader(reader));
			var r1 = l1.AsReadOnly();

			var r2 = new List<TResultSet2>(0).AsReadOnly();
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (r1, r2, new List<TResultSet3>(0).AsReadOnly(), new List<TResultSet4>(0).AsReadOnly(), new List<TResultSet5>(0).AsReadOnly());

			var l2 = new List<TResultSet2>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l2.Add(TResultSet2.MapFromDataReader(reader));
			r2 = l2.AsReadOnly();

			var r3 = new List<TResultSet3>(0).AsReadOnly();
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (r1, r2, r3, new List<TResultSet4>(0).AsReadOnly(), new List<TResultSet5>(0).AsReadOnly());

			var l3 = new List<TResultSet3>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l3.Add(TResultSet3.MapFromDataReader(reader));
			r3 = l3.AsReadOnly();

			var r4 = new List<TResultSet4>(0).AsReadOnly();
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (r1, r2, r3, r4, new List<TResultSet5>(0).AsReadOnly());

			var l4 = new List<TResultSet4>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l4.Add(TResultSet4.MapFromDataReader(reader));
			r4 = l4.AsReadOnly();

			var r5 = new List<TResultSet5>(0).AsReadOnly();
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (r1, r2, r3, r4, r5);

			var l5 = new List<TResultSet5>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				l5.Add(TResultSet5.MapFromDataReader(reader));
			r5 = l5.AsReadOnly();

			return (r1, r2, r3, r4, r5);
		}, cancellationToken).ConfigureAwait(false);
	}
}