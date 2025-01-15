namespace CaeriusNet.Commands.Reads;

/// <summary>
///     Contains methods for querying multiple result sets from a database and mapping them to IEnumerable collections.
/// </summary>
public static class MultiIEnumerableReadSqlAsyncCommands
{
	/// <summary>
	///     Asynchronously queries the database for two types of result sets and maps them to IEnumerable collections.
	/// </summary>
	/// <typeparam name="TResultSet1">The type of the first result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet2">The type of the second result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <param name="context">The database connection factory.</param>
	/// <param name="spParameters">The stored procedure parameters builder.</param>
	/// <param name="resultSet1">A function to map the first result set to <typeparamref name="TResultSet1" />.</param>
	/// <param name="resultSet2">A function to map the second result set to <typeparamref name="TResultSet2" />.</param>
	/// <returns>The task result is a tuple where each item is an IEnumerable of the mapped result sets.</returns>
	public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>)>
		QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2>(
			this ICaeriusDbContext context, StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> resultSet1, Func<SqlDataReader, TResultSet2> resultSet2)
		where TResultSet1 : class
		where TResultSet2 : class
	{
		var results = await context
			.ReadMultipleIEnumerableResultSetsAsync(spParameters, resultSet1, resultSet2);

		return (results[0].Cast<TResultSet1>(), results[1].Cast<TResultSet2>());
	}

	/// <summary>
	///     Asynchronously queries the database for three types of result sets and maps them to IEnumerable collections.
	/// </summary>
	/// <typeparam name="TResultSet1">The type of the first result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet2">The type of the second result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet3">The type of the third result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <param name="context">The database connection factory.</param>
	/// <param name="spParameters">The stored procedure parameters builder.</param>
	/// <param name="resultSet1">A function to map the first result set to <typeparamref name="TResultSet1" />.</param>
	/// <param name="resultSet2">A function to map the second result set to <typeparamref name="TResultSet2" />.</param>
	/// <param name="resultSet3">A function to map the third result set to <typeparamref name="TResultSet3" />.</param>
	/// <returns>The task result is a tuple where each item is an IEnumerable of the mapped result sets.</returns>
	public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>)>
		QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3>(
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
			.ReadMultipleIEnumerableResultSetsAsync(spParameters, resultSet1, resultSet2, resultSet3);

		return (results[0].Cast<TResultSet1>(), results[1].Cast<TResultSet2>(), results[2].Cast<TResultSet3>());
	}

	/// <summary>
	///     Asynchronously queries the database for four types of result sets and maps them to IEnumerable collections.
	/// </summary>
	/// <typeparam name="TResultSet1">The type of the first result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet2">The type of the second result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet3">The type of the third result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet4">The type of the fourth result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <param name="context">The database connection factory.</param>
	/// <param name="spParameters">The stored procedure parameters builder.</param>
	/// <param name="resultSet1">A function to map the first result set to <typeparamref name="TResultSet1" />.</param>
	/// <param name="resultSet2">A function to map the second result set to <typeparamref name="TResultSet2" />.</param>
	/// <param name="resultSet3">A function to map the third result set to <typeparamref name="TResultSet3" />.</param>
	/// <param name="resultSet4">A function to map the fourth result set to <typeparamref name="TResultSet4" />.</param>
	/// <returns>The task result is a tuple where each item is an IEnumerable of the mapped result sets.</returns>
	public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>,
			IEnumerable<TResultSet4>)>
		QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
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
			.ReadMultipleIEnumerableResultSetsAsync(spParameters, resultSet1, resultSet2, resultSet3, resultSet4);

		return (results[0].Cast<TResultSet1>(), results[1].Cast<TResultSet2>(), results[2].Cast<TResultSet3>(),
			results[3].Cast<TResultSet4>());
	}

	/// <summary>
	///     Asynchronously queries the database for five types of result sets and maps them to IEnumerable collections.
	/// </summary>
	/// <typeparam name="TResultSet1">The type of the first result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet2">The type of the second result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet3">The type of the third result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet4">The type of the fourth result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet5">The type of the fifth result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <param name="context">The database connection factory.</param>
	/// <param name="spParameters">The stored procedure parameters builder.</param>
	/// <param name="resultSet1">A function to map the first result set to <typeparamref name="TResultSet1" />.</param>
	/// <param name="resultSet2">A function to map the second result set to <typeparamref name="TResultSet2" />.</param>
	/// <param name="resultSet3">A function to map the third result set to <typeparamref name="TResultSet3" />.</param>
	/// <param name="resultSet4">A function to map the fourth result set to <typeparamref name="TResultSet4" />.</param>
	/// <param name="resultSet5">A function to map the fifth result set to <typeparamref name="TResultSet5" />.</param>
	/// <returns>The task result is a tuple where each item is an IEnumerable of the mapped result sets.</returns>
	public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>,
			IEnumerable<TResultSet4>, IEnumerable<TResultSet5>)>
		QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>(
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
		var results =
			await context.ReadMultipleIEnumerableResultSetsAsync(spParameters, resultSet1, resultSet2, resultSet3,
				resultSet4, resultSet5);

		return (results[0].Cast<TResultSet1>(), results[1].Cast<TResultSet2>(), results[2].Cast<TResultSet3>(),
			results[3].Cast<TResultSet4>(), results[4].Cast<TResultSet5>());
	}

	/// <summary>
	///     Asynchronously queries the database for multiple result sets and maps them to a list of IEnumerable collections.
	/// </summary>
	/// <param name="context">The database connection factory to create a connection.</param>
	/// <param name="spParameters">The stored procedure parameters builder to configure the command.</param>
	/// <param name="mappers">An array of functions to map each result set to a specific object type.</param>
	/// <returns>
	///     A task that represents the asynchronous operation. The task result contains a list of IEnumerable collections,
	///     where each collection represents a result set mapped to objects by the corresponding mapper function.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when no mapper functions are provided.</exception>
	private static async Task<List<IEnumerable<object>>> ReadMultipleIEnumerableResultSetsAsync(
		this ICaeriusDbContext context,
		StoredProcedureParameters spParameters,
		params Func<SqlDataReader, object>[] mappers)
	{
		if (mappers.Length == 0)
			throw new ArgumentException("At least one mapper function must be provided.", nameof(mappers));

		using var connection = context.DbConnection();
		await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
		await using var reader = await command.ExecuteReaderAsync();

		var results = new List<IEnumerable<object>>(mappers.Length);
		foreach (var mapper in mappers)
		{
			var resultSet = new List<object>();
			while (await reader.ReadAsync())
				resultSet.Add(mapper(reader));

			results.Add(resultSet.AsEnumerable());

			if (!await reader.NextResultAsync())
				break;
		}

		return results;
	}
}