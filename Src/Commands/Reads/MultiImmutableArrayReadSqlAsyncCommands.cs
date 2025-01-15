namespace CaeriusNet.Commands.Reads;

/// <summary>
///     Contains methods for querying multiple result sets from a database and mapping them to ImmutableArray collections.
/// </summary>
public static class MultiImmutableArrayReadSqlAsyncCommands
{
	/// <summary>
	///     Asynchronously queries the database for two result sets and maps them to a tuple of ImmutableArray collections.
	/// </summary>
	/// <typeparam name="TResultSet1">The type of objects in the first result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet2">The type of the second result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <param name="context">The factory to create a database connection.</param>
	/// <param name="spParameters">The parameters for the stored procedure.</param>
	/// <param name="map1">A function to map the first result set to <typeparamref name="TResultSet1" />.</param>
	/// <param name="map2">A function to map the second result set to <typeparamref name="TResultSet2" />.</param>
	/// <returns>The task result is a tuple where each item is an ImmutableArray of the mapped result sets.</returns>
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2>(
			this ICaeriusDbContext context,
			StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> map1,
			Func<SqlDataReader, TResultSet2> map2)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
	{
		var results =
			await context.ReadMultipleImmutableArrayResultSetsAsync(spParameters, map1, map2);

		return ([..results[0].Cast<TResultSet1>()],
			[..results[1].Cast<TResultSet2>()]);
	}

	/// <summary>
	///     Asynchronously queries the database for three result sets and maps them to a tuple of ImmutableArray collections.
	/// </summary>
	/// <typeparam name="TResultSet1">The type of the first result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet2">The type of the second result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <typeparam name="TResultSet3">The type of the third result set, must implement <see cref="ISpMapper{T}" />.</typeparam>
	/// <param name="context">The factory to create a database connection.</param>
	/// <param name="spParameters">The parameters for the stored procedure.</param>
	/// <param name="map1">A function to map the first result set to <typeparamref name="TResultSet1" />.</param>
	/// <param name="map2">A function to map the second result set to <typeparamref name="TResultSet2" />.</param>
	/// <param name="map3">A function to map the third result set to <typeparamref name="TResultSet3" />.</param>
	/// <returns>The task result is a tuple where each item is an ImmutableArray of the mapped result sets.</returns>
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3>(
			this ICaeriusDbContext context,
			StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> map1,
			Func<SqlDataReader, TResultSet2> map2,
			Func<SqlDataReader, TResultSet3> map3)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
	{
		var results =
			await context.ReadMultipleImmutableArrayResultSetsAsync(spParameters, map1, map2, map3);

		return ([..results[0].Cast<TResultSet1>()],
			[..results[1].Cast<TResultSet2>()],
			[..results[2].Cast<TResultSet3>()]);
	}

	/// <summary>
	///     Asynchronously queries the database for four result sets and maps them to a tuple of ImmutableArray collections.
	/// </summary>
	/// <typeparam name="TResultSet1">The type of objects in the first result set.</typeparam>
	/// <typeparam name="TResultSet2">The type of objects in the second result set.</typeparam>
	/// <typeparam name="TResultSet3">The type of objects in the third result set.</typeparam>
	/// <typeparam name="TResultSet4">The type of objects in the fourth result set.</typeparam>
	/// <param name="context">The factory to create a database connection.</param>
	/// <param name="spParameters">The parameters for the stored procedure.</param>
	/// <param name="map1">A function to map the first result set to <typeparamref name="TResultSet1" />.</param>
	/// <param name="map2">A function to map the second result set to <typeparamref name="TResultSet2" />.</param>
	/// <param name="map3">A function to map the third result set to <typeparamref name="TResultSet3" />.</param>
	/// <param name="map4">A function to map the fourth result set to <typeparamref name="TResultSet4" />.</param>
	/// <returns>The task result is a tuple where each item is an ImmutableArray of the mapped result sets.</returns>
	public static async Task<(ImmutableArray<TResultSet1>,
			ImmutableArray<TResultSet2>,
			ImmutableArray<TResultSet3>,
			ImmutableArray<TResultSet4>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
			this ICaeriusDbContext context,
			StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> map1,
			Func<SqlDataReader, TResultSet2> map2,
			Func<SqlDataReader, TResultSet3> map3,
			Func<SqlDataReader, TResultSet4> map4)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
		where TResultSet4 : class, ISpMapper<TResultSet4>
	{
		var results =
			await context.ReadMultipleImmutableArrayResultSetsAsync(spParameters, map1, map2, map3, map4);

		return ([..results[0].Cast<TResultSet1>()],
			[..results[1].Cast<TResultSet2>()],
			[..results[2].Cast<TResultSet3>()],
			[..results[3].Cast<TResultSet4>()]);
	}

	/// <summary>
	///     Asynchronously queries the database for five result sets and maps them to a tuple of ImmutableArray collections.
	/// </summary>
	/// <typeparam name="TResultSet1">The type of objects in the first result set.</typeparam>
	/// <typeparam name="TResultSet2">The type of objects in the second result set.</typeparam>
	/// <typeparam name="TResultSet3">The type of objects in the third result set.</typeparam>
	/// <typeparam name="TResultSet4">The type of objects in the fourth result set.</typeparam>
	/// <typeparam name="TResultSet5">The type of objects in the fifth result set.</typeparam>
	/// <param name="context">The factory to create a database connection.</param>
	/// <param name="spParameters">The parameters for the stored procedure.</param>
	/// <param name="map1">A function to map the first result set to <typeparamref name="TResultSet1" />.</param>
	/// <param name="map2">A function to map the second result set to <typeparamref name="TResultSet2" />.</param>
	/// <param name="map3">A function to map the third result set to <typeparamref name="TResultSet3" />.</param>
	/// <param name="map4">A function to map the fourth result set to <typeparamref name="TResultSet4" />.</param>
	/// <param name="map5">A function to map the fifth result set to <typeparamref name="TResultSet5" />.</param>
	/// <returns>The task result is a tuple where each item is an ImmutableArray of the mapped result sets.</returns>
	/// <exception cref="ArgumentException">Thrown when no mapper functions are provided.</exception>
	public static async
		Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>,
			ImmutableArray<TResultSet4>,
			ImmutableArray<TResultSet5>)> QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3,
			TResultSet4, TResultSet5>(
			this ICaeriusDbContext context,
			StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> map1,
			Func<SqlDataReader, TResultSet2> map2,
			Func<SqlDataReader, TResultSet3> map3,
			Func<SqlDataReader, TResultSet4> map4,
			Func<SqlDataReader, TResultSet5> map5)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
		where TResultSet4 : class, ISpMapper<TResultSet4>
		where TResultSet5 : class, ISpMapper<TResultSet5>
	{
		var results =
			await context.ReadMultipleImmutableArrayResultSetsAsync(spParameters, map1, map2, map3, map4,
				map5);

		return ([..results[0].Cast<TResultSet1>()],
			[..results[1].Cast<TResultSet2>()],
			[..results[2].Cast<TResultSet3>()],
			[..results[3].Cast<TResultSet4>()],
			[..results[4].Cast<TResultSet5>()]);
	}

	/// <summary>
	///     Asynchronously queries the database for multiple result sets and maps them to a list of ImmutableArray collections.
	/// </summary>
	/// <param name="context">The database connection factory to create a connection.</param>
	/// <param name="spParameters">The stored procedure parameters builder to configure the command.</param>
	/// <param name="mappers">An array of functions to map each result set to a specific object type.</param>
	/// <returns>
	///     A task that represents the asynchronous operation. The task result contains a list of ImmutableArray collections,
	///     where each collection represents a result set mapped to objects by the corresponding mapper function.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when no mapper functions are provided.</exception>
	/// <remarks>
	///     This method is designed to efficiently handle multiple result sets from a single database query,
	///     converting each result set into a strongly-typed ImmutableArray based on the provided mapper functions.
	/// </remarks>
	private static async Task<List<ImmutableArray<object>>> ReadMultipleImmutableArrayResultSetsAsync(
		this ICaeriusDbContext context,
		StoredProcedureParameters spParameters,
		params Func<SqlDataReader, object>[] mappers)
	{
		if (mappers.Length == 0)
			throw new ArgumentException("At least one mapper function must be provided.", nameof(mappers));

		using var connection = context.DbConnection();
		await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
		await using var reader = await command.ExecuteReaderAsync();

		var results = new List<ImmutableArray<object>>(mappers.Length);
		foreach (var mapper in mappers)
		{
			var resultSet = ImmutableArray.CreateBuilder<object>();
			while (await reader.ReadAsync())
				resultSet.Add(mapper(reader));

			results.Add(resultSet.ToImmutable());

			if (!await reader.NextResultAsync())
				break;
		}

		return results;
	}
}