namespace CaeriusNet.Commands.Reads;

/// <summary>
///     Contains methods for querying multiple result sets from a database and mapping them to ReadOnlyCollection
///     collections.
/// </summary>
public static class MultiReadOnlyCollectionReadSqlAsyncCommands
{
	/// <summary>
	///     Asynchronously queries the database for two result sets and maps them to ReadOnlyCollection collections.
	/// </summary>
	/// <typeparam name="TResultSet1">The type of objects in the first result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet2">The type of objects in the second result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <param name="context">The database connection factory used to create a new database connection.</param>
	/// <param name="spParameters">The stored procedure parameters builder used to configure the command.</param>
	/// <param name="resultSet1">A function to map the first result set to <typeparamref name="TResultSet1" />.</param>
	/// <param name="resultSet2">A function to map the second result set to <typeparamref name="TResultSet2" />.</param>
	/// <returns>The task result is a tuple where each item is an IReadOnlyCollection of the result set objects.</returns>
	public static async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>)>
		QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2>(
			this ICaeriusDbContext context,
			StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> resultSet1,
			Func<SqlDataReader, TResultSet2> resultSet2)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
	{
		var results =
			await context.ReadMultipleIReadOnlyCollectionResultSetsAsync(spParameters, resultSet1, resultSet2);

		return (new ReadOnlyCollection<TResultSet1>(results[0].Cast<TResultSet1>().ToList()),
			new ReadOnlyCollection<TResultSet2>(results[1].Cast<TResultSet2>().ToList()));
	}

	public static async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>,
			ReadOnlyCollection<TResultSet3>)>
		QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2, TResultSet3>(
			this ICaeriusDbContext context,
			StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> resultSet1,
			Func<SqlDataReader, TResultSet2> resultSet2,
			Func<SqlDataReader, TResultSet3> resultSet3)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
	{
		var results = await context
			.ReadMultipleIReadOnlyCollectionResultSetsAsync(spParameters, resultSet1, resultSet2, resultSet3);

		return (new ReadOnlyCollection<TResultSet1>(results[0].Cast<TResultSet1>().ToList()),
			new ReadOnlyCollection<TResultSet2>(results[1].Cast<TResultSet2>().ToList()),
			new ReadOnlyCollection<TResultSet3>(results[2].Cast<TResultSet3>().ToList()));
	}

	public static async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>,
			ReadOnlyCollection<TResultSet3>, ReadOnlyCollection<TResultSet4>)>
		QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
			this ICaeriusDbContext context,
			StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> resultSet1,
			Func<SqlDataReader, TResultSet2> resultSet2,
			Func<SqlDataReader, TResultSet3> resultSet3,
			Func<SqlDataReader, TResultSet4> resultSet4)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
		where TResultSet4 : class, ISpMapper<TResultSet4>
	{
		var results = await context
			.ReadMultipleIReadOnlyCollectionResultSetsAsync(spParameters, resultSet1, resultSet2, resultSet3,
				resultSet4);

		return (new ReadOnlyCollection<TResultSet1>(results[0].Cast<TResultSet1>().ToList()),
			new ReadOnlyCollection<TResultSet2>(results[1].Cast<TResultSet2>().ToList()),
			new ReadOnlyCollection<TResultSet3>(results[2].Cast<TResultSet3>().ToList()),
			new ReadOnlyCollection<TResultSet4>(results[3].Cast<TResultSet4>().ToList()));
	}

	public static async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>,
			ReadOnlyCollection<TResultSet3>, ReadOnlyCollection<TResultSet4>, ReadOnlyCollection<TResultSet5>)>
		QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>(
			this ICaeriusDbContext context,
			StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> resultSet1,
			Func<SqlDataReader, TResultSet2> resultSet2,
			Func<SqlDataReader, TResultSet3> resultSet3,
			Func<SqlDataReader, TResultSet4> resultSet4,
			Func<SqlDataReader, TResultSet5> resultSet5)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
		where TResultSet4 : class, ISpMapper<TResultSet4>
		where TResultSet5 : class, ISpMapper<TResultSet5>
	{
		var results = await context
			.ReadMultipleIReadOnlyCollectionResultSetsAsync(spParameters, resultSet1, resultSet2, resultSet3,
				resultSet4, resultSet5);

		return (new ReadOnlyCollection<TResultSet1>(results[0].Cast<TResultSet1>().ToList()),
			new ReadOnlyCollection<TResultSet2>(results[1].Cast<TResultSet2>().ToList()),
			new ReadOnlyCollection<TResultSet3>(results[2].Cast<TResultSet3>().ToList()),
			new ReadOnlyCollection<TResultSet4>(results[3].Cast<TResultSet4>().ToList()),
			new ReadOnlyCollection<TResultSet5>(results[4].Cast<TResultSet5>().ToList()));
	}

	private static async Task<List<IReadOnlyCollection<object>>> ReadMultipleIReadOnlyCollectionResultSetsAsync(
		this ICaeriusDbContext context, StoredProcedureParameters spParameters,
		params Func<SqlDataReader, object>[] mappers)
	{
		if (mappers.Length == 0)
			throw new ArgumentException("At least one mapper function must be provided.", nameof(mappers));

		using var connection = context.DbConnection();
		await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
		await using var reader = await command.ExecuteReaderAsync();

		var results = new List<IReadOnlyCollection<object>>(mappers.Length);
		foreach (var mapper in mappers)
		{
			var items = new List<object>(spParameters.Capacity);
			while (await reader.ReadAsync())
				items.Add(mapper(reader));
			results.Add(items);

			if (!await reader.NextResultAsync())
				break;
		}

		return results;
	}
}