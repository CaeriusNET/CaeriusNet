namespace CaeriusNet.Commands.Reads;

public static class MultiReadOnlyCollectionReadSqlAsyncCommands
{
	/// <summary>
	///     Executes a stored procedure to retrieve two result sets, mapping each into a read-only collection of the specified
	///     types.
	/// </summary>
	/// <typeparam name="TResultSet1">
	///     The type of the objects in the first result set, implementing <see cref="ISpMapper{T}" />
	///     .
	/// </typeparam>
	/// <typeparam name="TResultSet2">
	///     The type of the objects in the second result set, implementing
	///     <see cref="ISpMapper{T}" />.
	/// </typeparam>
	/// <param name="dbContext">The database context providing the connection to execute the stored procedure.</param>
	/// <param name="spParameters">
	///     The parameters for the stored procedure, including name, parameters, and optional caching
	///     details.
	/// </param>
	/// <param name="resultSet1">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet1" />.
	/// </param>
	/// <param name="resultSet2">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet2" />.
	/// </param>
	/// <returns>
	///     A tuple containing two read-only collections: the first for objects of type
	///     <typeparamref name="TResultSet1" /> and the second for objects of type <typeparamref name="TResultSet2" />.
	/// </returns>
	public static async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>)>
		QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2>(
			this ICaeriusDbContext dbContext, StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> resultSet1,
			Func<SqlDataReader, TResultSet2> resultSet2)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
	{
		var results = await SqlCommandUtility.ExecuteMultipleReadOnlyResultSetsAsync(
			spParameters, dbContext.DbConnection(),
			resultSet1, resultSet2);

		return (
			new ReadOnlyCollection<TResultSet1>(results[0].Cast<TResultSet1>().ToList()),
			new ReadOnlyCollection<TResultSet2>(results[1].Cast<TResultSet2>().ToList()));
	}

	/// <summary>
	///     Executes a stored procedure to retrieve three result sets, mapping each into a read-only collection of the
	///     specified types.
	/// </summary>
	/// <typeparam name="TResultSet1">
	///     The type of the objects in the first result set, implementing <see cref="ISpMapper{T}" />
	///     .
	/// </typeparam>
	/// <typeparam name="TResultSet2">
	///     The type of the objects in the second result set, implementing
	///     <see cref="ISpMapper{T}" />.
	/// </typeparam>
	/// <typeparam name="TResultSet3">
	///     The type of the objects in the third result set, implementing <see cref="ISpMapper{T}" />
	///     .
	/// </typeparam>
	/// <param name="dbContext">The database context providing the connection to execute the stored procedure.</param>
	/// <param name="spParameters">
	///     The parameters for the stored procedure, including name, parameters, and optional caching
	///     details.
	/// </param>
	/// <param name="resultSet1">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet1" />.
	/// </param>
	/// <param name="resultSet2">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet2" />.
	/// </param>
	/// <param name="resultSet3">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet3" />.
	/// </param>
	/// <returns>
	///     A tuple containing three read-only collections: the first for objects of type
	///     <typeparamref name="TResultSet1" />, the second for objects of type <typeparamref name="TResultSet2" />, and the
	///     third for objects of type <typeparamref name="TResultSet3" />.
	/// </returns>
	public static async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>,
			ReadOnlyCollection<TResultSet3>)>
		QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2, TResultSet3>(
			this ICaeriusDbContext dbContext, StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> resultSet1,
			Func<SqlDataReader, TResultSet2> resultSet2,
			Func<SqlDataReader, TResultSet3> resultSet3)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
	{
		var results = await SqlCommandUtility.ExecuteMultipleReadOnlyResultSetsAsync(
			spParameters, dbContext.DbConnection(),
			resultSet1, resultSet2, resultSet3);

		return (
			new ReadOnlyCollection<TResultSet1>(results[0].Cast<TResultSet1>().ToList()),
			new ReadOnlyCollection<TResultSet2>(results[1].Cast<TResultSet2>().ToList()),
			new ReadOnlyCollection<TResultSet3>(results[2].Cast<TResultSet3>().ToList()));
	}

	/// <summary>
	///     Executes a stored procedure to retrieve four result sets, mapping each into a read-only collection of the specified
	///     types.
	/// </summary>
	/// <typeparam name="TResultSet1">
	///     The type of the objects in the first result set, implementing <see cref="ISpMapper{T}" />
	///     .
	/// </typeparam>
	/// <typeparam name="TResultSet2">
	///     The type of the objects in the second result set, implementing
	///     <see cref="ISpMapper{T}" />.
	/// </typeparam>
	/// <typeparam name="TResultSet3">
	///     The type of the objects in the third result set, implementing <see cref="ISpMapper{T}" />
	///     .
	/// </typeparam>
	/// <typeparam name="TResultSet4">
	///     The type of the objects in the fourth result set, implementing
	///     <see cref="ISpMapper{T}" />.
	/// </typeparam>
	/// <param name="dbContext">The database context providing the connection to execute the stored procedure.</param>
	/// <param name="spParameters">
	///     The parameters for the stored procedure, including name, parameters, and optional caching
	///     details.
	/// </param>
	/// <param name="resultSet1">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet1" />.
	/// </param>
	/// <param name="resultSet2">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet2" />.
	/// </param>
	/// <param name="resultSet3">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet3" />.
	/// </param>
	/// <param name="resultSet4">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet4" />.
	/// </param>
	/// <returns>
	///     A tuple containing four read-only collections: the first for objects of type
	///     <typeparamref name="TResultSet1" />, the second for objects of type <typeparamref name="TResultSet2" />, the third
	///     for objects of type <typeparamref name="TResultSet3" />, and the fourth for objects of type
	///     <typeparamref name="TResultSet4" />.
	/// </returns>
	public static async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>,
			ReadOnlyCollection<TResultSet3>, ReadOnlyCollection<TResultSet4>)>
		QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
			this ICaeriusDbContext dbContext, StoredProcedureParameters spParameters,
			Func<SqlDataReader, TResultSet1> resultSet1,
			Func<SqlDataReader, TResultSet2> resultSet2,
			Func<SqlDataReader, TResultSet3> resultSet3,
			Func<SqlDataReader, TResultSet4> resultSet4)
		where TResultSet1 : class, ISpMapper<TResultSet1>
		where TResultSet2 : class, ISpMapper<TResultSet2>
		where TResultSet3 : class, ISpMapper<TResultSet3>
		where TResultSet4 : class, ISpMapper<TResultSet4>
	{
		var results = await SqlCommandUtility.ExecuteMultipleReadOnlyResultSetsAsync(
			spParameters, dbContext.DbConnection(),
			resultSet1, resultSet2, resultSet3, resultSet4);

		return (
			new ReadOnlyCollection<TResultSet1>(results[0].Cast<TResultSet1>().ToList()),
			new ReadOnlyCollection<TResultSet2>(results[1].Cast<TResultSet2>().ToList()),
			new ReadOnlyCollection<TResultSet3>(results[2].Cast<TResultSet3>().ToList()),
			new ReadOnlyCollection<TResultSet4>(results[3].Cast<TResultSet4>().ToList()));
	}

	/// <summary>
	///     Executes a stored procedure to retrieve five result sets, mapping each into a read-only collection of the specified
	///     types.
	/// </summary>
	/// <typeparam name="TResultSet1">
	///     The type of the objects in the first result set, implementing <see cref="ISpMapper{T}" />
	///     .
	/// </typeparam>
	/// <typeparam name="TResultSet2">
	///     The type of the objects in the second result set, implementing
	///     <see cref="ISpMapper{T}" />.
	/// </typeparam>
	/// <typeparam name="TResultSet3">
	///     The type of the objects in the third result set, implementing <see cref="ISpMapper{T}" />
	///     .
	/// </typeparam>
	/// <typeparam name="TResultSet4">
	///     The type of the objects in the fourth result set, implementing
	///     <see cref="ISpMapper{T}" />.
	/// </typeparam>
	/// <typeparam name="TResultSet5">
	///     The type of the objects in the fifth result set, implementing <see cref="ISpMapper{T}" />
	///     .
	/// </typeparam>
	/// <param name="dbContext">The database context providing the connection to execute the stored procedure.</param>
	/// <param name="spParameters">
	///     The parameters for the stored procedure, including name, parameters, and optional caching
	///     details.
	/// </param>
	/// <param name="resultSet1">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet1" />.
	/// </param>
	/// <param name="resultSet2">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet2" />.
	/// </param>
	/// <param name="resultSet3">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet3" />.
	/// </param>
	/// <param name="resultSet4">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet4" />.
	/// </param>
	/// <param name="resultSet5">
	///     The function to map data from a <see cref="SqlDataReader" /> to instances of
	///     <typeparamref name="TResultSet5" />.
	/// </param>
	/// <returns>
	///     A tuple containing five read-only collections:
	///     the first for objects of type <typeparamref name="TResultSet1" />,
	///     the second for objects of type <typeparamref name="TResultSet2" />,
	///     the third for objects of type <typeparamref name="TResultSet3" />,
	///     the fourth for objects of type <typeparamref name="TResultSet4" />,
	///     and the fifth for objects of type <typeparamref name="TResultSet5" />.
	/// </returns>
	public static async Task<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>,
			ReadOnlyCollection<TResultSet3>, ReadOnlyCollection<TResultSet4>, ReadOnlyCollection<TResultSet5>)>
		QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>(
			this ICaeriusDbContext dbContext, StoredProcedureParameters spParameters,
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
		var results = await SqlCommandUtility.ExecuteMultipleReadOnlyResultSetsAsync(
			spParameters, dbContext.DbConnection(),
			resultSet1, resultSet2, resultSet3, resultSet4, resultSet5);

		return (
			new ReadOnlyCollection<TResultSet1>(results[0].Cast<TResultSet1>().ToList()),
			new ReadOnlyCollection<TResultSet2>(results[1].Cast<TResultSet2>().ToList()),
			new ReadOnlyCollection<TResultSet3>(results[2].Cast<TResultSet3>().ToList()),
			new ReadOnlyCollection<TResultSet4>(results[3].Cast<TResultSet4>().ToList()),
			new ReadOnlyCollection<TResultSet5>(results[4].Cast<TResultSet5>().ToList()));
	}
}