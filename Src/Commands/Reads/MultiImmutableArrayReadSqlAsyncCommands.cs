namespace CaeriusNet.Commands.Reads;

/// <summary>
///     Provides asynchronous methods for executing database queries that return multiple result sets,
///     each mapped to an ImmutableArray of strongly typed objects.
/// </summary>
public static class MultiImmutableArrayReadSqlAsyncCommands
{
	/// Executes a stored procedure and maps the results into multiple immutable arrays
	/// of specified result types.
	/// <typeparam name="TResultSet1">The type of the first result set.</typeparam>
	/// <typeparam name="TResultSet2">The type of the second result set.</typeparam>
	/// <param name="context">The database context for executing the query.</param>
	/// <param name="spParameters">The parameters required to execute the stored procedure.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A tuple containing two immutable arrays, one for each result set.</returns>
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2>(
			this ICaeriusDbContext context,
			StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
	{
		return await SqlCommandUtility.ExecuteCommandAsync(context, spParameters, execute: async command => {
			await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

			var b1 = ImmutableArray.CreateBuilder<TResultSet1>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b1.Add(TResultSet1.MapFromDataReader(reader));
			var a1 = b1.ToImmutable();

			var a2 = ImmutableArray<TResultSet2>.Empty;
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (a1, a2);

			var b2 = ImmutableArray.CreateBuilder<TResultSet2>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b2.Add(TResultSet2.MapFromDataReader(reader));
			a2 = b2.ToImmutable();

			return (a1, a2);
		}, cancellationToken).ConfigureAwait(false);
	}

	/// Executes a stored procedure and maps the results into multiple immutable arrays
	/// containing three specified result types.
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3>(
			this ICaeriusDbContext context,
			StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
	{
		return await SqlCommandUtility.ExecuteCommandAsync(context, spParameters, execute: async command => {
			await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

			var b1 = ImmutableArray.CreateBuilder<TResultSet1>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b1.Add(TResultSet1.MapFromDataReader(reader));
			var a1 = b1.ToImmutable();

			var a2 = ImmutableArray<TResultSet2>.Empty;
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (a1, a2, ImmutableArray<TResultSet3>.Empty);

			var b2 = ImmutableArray.CreateBuilder<TResultSet2>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b2.Add(TResultSet2.MapFromDataReader(reader));
			a2 = b2.ToImmutable();

			var a3 = ImmutableArray<TResultSet3>.Empty;
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (a1, a2, a3);

			var b3 = ImmutableArray.CreateBuilder<TResultSet3>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b3.Add(TResultSet3.MapFromDataReader(reader));
			a3 = b3.ToImmutable();

			return (a1, a2, a3);
		}, cancellationToken).ConfigureAwait(false);
	}

	/// Executes a stored procedure and maps the results into multiple immutable arrays (4 sets).
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>,
			ImmutableArray<TResultSet4>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
			this ICaeriusDbContext context,
			StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
		where TResultSet4 : class, ISpMapper<TResultSet4>
	{
		return await SqlCommandUtility.ExecuteCommandAsync(context, spParameters, execute: async command => {
			await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

			var b1 = ImmutableArray.CreateBuilder<TResultSet1>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b1.Add(TResultSet1.MapFromDataReader(reader));
			var a1 = b1.ToImmutable();

			var a2 = ImmutableArray<TResultSet2>.Empty;
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (a1, a2, ImmutableArray<TResultSet3>.Empty, ImmutableArray<TResultSet4>.Empty);

			var b2 = ImmutableArray.CreateBuilder<TResultSet2>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b2.Add(TResultSet2.MapFromDataReader(reader));
			a2 = b2.ToImmutable();

			var a3 = ImmutableArray<TResultSet3>.Empty;
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (a1, a2, a3, ImmutableArray<TResultSet4>.Empty);

			var b3 = ImmutableArray.CreateBuilder<TResultSet3>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b3.Add(TResultSet3.MapFromDataReader(reader));
			a3 = b3.ToImmutable();

			var a4 = ImmutableArray<TResultSet4>.Empty;
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (a1, a2, a3, a4);

			var b4 = ImmutableArray.CreateBuilder<TResultSet4>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b4.Add(TResultSet4.MapFromDataReader(reader));
			a4 = b4.ToImmutable();

			return (a1, a2, a3, a4);
		}, cancellationToken).ConfigureAwait(false);
	}

	/// Executes a stored procedure and maps the results into multiple immutable arrays (5 sets).
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>,
			ImmutableArray<TResultSet4>, ImmutableArray<TResultSet5>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>(
			this ICaeriusDbContext context,
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

			var b1 = ImmutableArray.CreateBuilder<TResultSet1>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b1.Add(TResultSet1.MapFromDataReader(reader));
			var a1 = b1.ToImmutable();

			var a2 = ImmutableArray<TResultSet2>.Empty;
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (a1, a2, ImmutableArray<TResultSet3>.Empty, ImmutableArray<TResultSet4>.Empty, ImmutableArray<TResultSet5>.Empty);

			var b2 = ImmutableArray.CreateBuilder<TResultSet2>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b2.Add(TResultSet2.MapFromDataReader(reader));
			a2 = b2.ToImmutable();

			var a3 = ImmutableArray<TResultSet3>.Empty;
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (a1, a2, a3, ImmutableArray<TResultSet4>.Empty, ImmutableArray<TResultSet5>.Empty);

			var b3 = ImmutableArray.CreateBuilder<TResultSet3>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b3.Add(TResultSet3.MapFromDataReader(reader));
			a3 = b3.ToImmutable();

			var a4 = ImmutableArray<TResultSet4>.Empty;
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (a1, a2, a3, a4, ImmutableArray<TResultSet5>.Empty);

			var b4 = ImmutableArray.CreateBuilder<TResultSet4>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b4.Add(TResultSet4.MapFromDataReader(reader));
			a4 = b4.ToImmutable();

			var a5 = ImmutableArray<TResultSet5>.Empty;
			if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
				return (a1, a2, a3, a4, a5);

			var b5 = ImmutableArray.CreateBuilder<TResultSet5>(spParameters.Capacity);
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				b5.Add(TResultSet5.MapFromDataReader(reader));
			a5 = b5.ToImmutable();

			return (a1, a2, a3, a4, a5);
		}, cancellationToken).ConfigureAwait(false);
	}
}