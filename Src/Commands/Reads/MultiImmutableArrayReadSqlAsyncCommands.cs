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
	/// <param name="resultSet1">A function that maps the first result set from the SqlDataReader.</param>
	/// <param name="resultSet2">A function that maps the second result set from the SqlDataReader.</param>
	/// <returns>A tuple containing two immutable arrays, one for each result set.</returns>
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2>(
			this ICaeriusDbContext context,
			StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> resultSet1,
			Func<SqlDataReader, TResultSet2> resultSet2)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
	{
		var results = await SqlCommandUtility.ExecuteMultipleResultSetsAsync(
			spParameters, context.DbConnection(),
			resultSet1, resultSet2);

		return (
			[..results[0].Cast<TResultSet1>()],
			[..results[1].Cast<TResultSet2>()]);
	}

	/// Executes a stored procedure and maps the results into multiple immutable arrays
	/// containing three specified result types.
	/// <typeparam name="TResultSet1">The type of the first result set.</typeparam>
	/// <typeparam name="TResultSet2">The type of the second result set.</typeparam>
	/// <typeparam name="TResultSet3">The type of the third result set.</typeparam>
	/// <param name="context">The database context for executing the query.</param>
	/// <param name="spParameters">The parameters required to execute the stored procedure.</param>
	/// <param name="resultSet1">A function that maps the first result set from the SqlDataReader.</param>
	/// <param name="resultSet2">A function that maps the second result set from the SqlDataReader.</param>
	/// <param name="resultSet3">A function that maps the third result set from the SqlDataReader.</param>
	/// <returns>A tuple containing three immutable arrays, one for each result set.</returns>
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3>(
			this ICaeriusDbContext context,
			StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> resultSet1,
			Func<SqlDataReader, TResultSet2> resultSet2,
			Func<SqlDataReader, TResultSet3> resultSet3)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
	{
		var results = await SqlCommandUtility.ExecuteMultipleResultSetsAsync(
			spParameters, context.DbConnection(),
			resultSet1, resultSet2, resultSet3);

		return (
			[..results[0].Cast<TResultSet1>()],
			[..results[1].Cast<TResultSet2>()],
			[..results[2].Cast<TResultSet3>()]);
	}

	/// Executes a stored procedure and maps the results into multiple immutable arrays
	/// of specified result types.
	/// <typeparam name="TResultSet1">The type of the first result set.</typeparam>
	/// <typeparam name="TResultSet2">The type of the second result set.</typeparam>
	/// <typeparam name="TResultSet3">The type of the third result set.</typeparam>
	/// <typeparam name="TResultSet4">The type of the fourth result set.</typeparam>
	/// <param name="context">The database context for executing the query.</param>
	/// <param name="spParameters">The parameters required to execute the stored procedure.</param>
	/// <param name="resultSet1">A function that maps the first result set from the SqlDataReader.</param>
	/// <param name="resultSet2">A function that maps the second result set from the SqlDataReader.</param>
	/// <param name="resultSet3">A function that maps the third result set from the SqlDataReader.</param>
	/// <param name="resultSet4">A function that maps the fourth result set from the SqlDataReader.</param>
	/// <returns>A tuple containing four immutable arrays, one for each result set.</returns>
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>,
			ImmutableArray<TResultSet4>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
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
		var results = await SqlCommandUtility.ExecuteMultipleResultSetsAsync(
			spParameters, context.DbConnection(),
			resultSet1, resultSet2, resultSet3, resultSet4);

		return (
			[..results[0].Cast<TResultSet1>()],
			[..results[1].Cast<TResultSet2>()],
			[..results[2].Cast<TResultSet3>()],
			[..results[3].Cast<TResultSet4>()]);
	}

	/// Executes a stored procedure and maps the results into multiple immutable arrays
	/// of specified result types.
	/// <typeparam name="TResultSet1">The type of the first result set.</typeparam>
	/// <typeparam name="TResultSet2">The type of the second result set.</typeparam>
	/// <typeparam name="TResultSet3">The type of the third result set.</typeparam>
	/// <typeparam name="TResultSet4">The type of the fourth result set.</typeparam>
	/// <typeparam name="TResultSet5">The type of the fifth result set.</typeparam>
	/// <param name="context">The database context for executing the query.</param>
	/// <param name="spParameters">The parameters required to execute the stored procedure.</param>
	/// <param name="resultSet1">A function that maps the first result set from the SqlDataReader.</param>
	/// <param name="resultSet2">A function that maps the second result set from the SqlDataReader.</param>
	/// <param name="resultSet3">A function that maps the third result set from the SqlDataReader.</param>
	/// <param name="resultSet4">A function that maps the fourth result set from the SqlDataReader.</param>
	/// <param name="resultSet5">A function that maps the fifth result set from the SqlDataReader.</param>
	/// <returns>A tuple containing five immutable arrays, one for each result set.</returns>
	public static async Task<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>,
			ImmutableArray<TResultSet4>, ImmutableArray<TResultSet5>)>
		QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>(
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
		var results = await SqlCommandUtility.ExecuteMultipleResultSetsAsync(
			spParameters, context.DbConnection(),
			resultSet1, resultSet2, resultSet3, resultSet4, resultSet5);

		return (
			[..results[0].Cast<TResultSet1>()],
			[..results[1].Cast<TResultSet2>()],
			[..results[2].Cast<TResultSet3>()],
			[..results[3].Cast<TResultSet4>()],
			[..results[4].Cast<TResultSet5>()]);
	}
}