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
	/// <typeparam name="TResultSet1">The type of the objects in the first result set.</typeparam>
	/// <typeparam name="TResultSet2">The type of the objects in the second result set.</typeparam>
	/// <param name="context">The database context to use for executing the stored procedure.</param>
	/// <param name="spParameters">
	///     The parameters required for the stored procedure, including the procedure name and SQL
	///     parameters.
	/// </param>
	/// <param name="resultSet1">
	///     The mapping function to convert rows in the first result set to objects of type
	///     <typeparamref name="TResultSet1" />.
	/// </param>
	/// <param name="resultSet2">
	///     The mapping function to convert rows in the second result set to objects of type
	///     <typeparamref name="TResultSet2" />.
	/// </param>
	/// <returns>
	///     A tuple containing two enumerable collections: the first collection contains objects of type
	///     <typeparamref name="TResultSet1" />,
	///     and the second contains objects of type <typeparamref name="TResultSet2" />.
	/// </returns>
	public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>)>
        QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2>(
            this ICaeriusNetDbContext context,
            StoredProcedureParameters spParameters,
            Func<SqlDataReader, TResultSet1> resultSet1,
            Func<SqlDataReader, TResultSet2> resultSet2)
        where TResultSet1 : class, ISpMapper<TResultSet1>
        where TResultSet2 : class, ISpMapper<TResultSet2>
    {
        var results = await SqlCommandUtility.ExecuteMultipleIEnumerableResultSetsAsync(
            spParameters, context.DbConnection(),
            resultSet1, resultSet2);

        return (
            results[0].Cast<TResultSet1>(),
            results[1].Cast<TResultSet2>());
    }


	/// <summary>
	///     Executes a stored procedure and returns three result sets as enumerable collections.
	/// </summary>
	/// <typeparam name="TResultSet1">The type of the objects in the first result set.</typeparam>
	/// <typeparam name="TResultSet2">The type of the objects in the second result set.</typeparam>
	/// <typeparam name="TResultSet3">The type of the objects in the third result set.</typeparam>
	/// <param name="context">The database context to use for executing the stored procedure.</param>
	/// <param name="spParameters">
	///     The parameters required for the stored procedure, including the procedure name and SQL
	///     parameters.
	/// </param>
	/// <param name="resultSet1">
	///     The mapping function to convert rows in the first result set to objects of type
	///     <typeparamref name="TResultSet1" />.
	/// </param>
	/// <param name="resultSet2">
	///     The mapping function to convert rows in the second result set to objects of type
	///     <typeparamref name="TResultSet2" />.
	/// </param>
	/// <param name="resultSet3">
	///     The mapping function to convert rows in the third result set to objects of type
	///     <typeparamref name="TResultSet3" />.
	/// </param>
	/// <returns>
	///     A tuple containing three enumerable collections: the first collection contains objects of type
	///     <typeparamref name="TResultSet1" />, the second contains objects of type <typeparamref name="TResultSet2" />,
	///     and the third contains objects of type <typeparamref name="TResultSet3" />.
	/// </returns>
	public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>)>
        QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3>(
            this ICaeriusNetDbContext context,
            StoredProcedureParameters spParameters,
            Func<SqlDataReader, TResultSet1> resultSet1,
            Func<SqlDataReader, TResultSet2> resultSet2,
            Func<SqlDataReader, TResultSet3> resultSet3)
        where TResultSet1 : class, ISpMapper<TResultSet1>
        where TResultSet2 : class, ISpMapper<TResultSet2>
        where TResultSet3 : class, ISpMapper<TResultSet3>
    {
        var results = await SqlCommandUtility.ExecuteMultipleIEnumerableResultSetsAsync(
            spParameters, context.DbConnection(),
            resultSet1, resultSet2, resultSet3);

        return (
            results[0].Cast<TResultSet1>(),
            results[1].Cast<TResultSet2>(),
            results[2].Cast<TResultSet3>());
    }

	/// <summary>
	///     Executes a stored procedure and returns multiple result sets as enumerable collections.
	/// </summary>
	/// <typeparam name="TResultSet1">The type of the objects in the first result set.</typeparam>
	/// <typeparam name="TResultSet2">The type of the objects in the second result set.</typeparam>
	/// <typeparam name="TResultSet3">The type of the objects in the third result set.</typeparam>
	/// <typeparam name="TResultSet4">The type of the objects in the fourth result set.</typeparam>
	/// <param name="context">The database context to use for executing the stored procedure.</param>
	/// <param name="spParameters">
	///     The parameters required for the stored procedure, including the procedure name and SQL
	///     parameters.
	/// </param>
	/// <param name="resultSet1">
	///     The mapping function to convert rows in the first result set to objects of type
	///     <typeparamref name="TResultSet1" />.
	/// </param>
	/// <param name="resultSet2">
	///     The mapping function to convert rows in the second result set to objects of type
	///     <typeparamref name="TResultSet2" />.
	/// </param>
	/// <param name="resultSet3">
	///     The mapping function to convert rows in the third result set to objects of type
	///     <typeparamref name="TResultSet3" />.
	/// </param>
	/// <param name="resultSet4">
	///     The mapping function to convert rows in the fourth result set to objects of type
	///     <typeparamref name="TResultSet4" />.
	/// </param>
	/// <returns>
	///     A tuple containing four enumerable collections:
	///     the first collection contains objects of type <typeparamref name="TResultSet1" />,
	///     the second contains objects of type <typeparamref name="TResultSet2" />,
	///     the third contains objects of type <typeparamref name="TResultSet3" />,
	///     and the fourth contains objects of type <typeparamref name="TResultSet4" />.
	/// </returns>
	public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>,
            IEnumerable<TResultSet4>)>
        QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
            this ICaeriusNetDbContext context,
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
        var results = await SqlCommandUtility.ExecuteMultipleIEnumerableResultSetsAsync(
            spParameters, context.DbConnection(),
            resultSet1, resultSet2, resultSet3, resultSet4);

        return (
            results[0].Cast<TResultSet1>(),
            results[1].Cast<TResultSet2>(),
            results[2].Cast<TResultSet3>(),
            results[3].Cast<TResultSet4>());
    }

	/// <summary>
	///     Executes a stored procedure and retrieves five result sets as enumerable collections.
	/// </summary>
	/// <typeparam name="TResultSet1">The type of the objects in the first result set.</typeparam>
	/// <typeparam name="TResultSet2">The type of the objects in the second result set.</typeparam>
	/// <typeparam name="TResultSet3">The type of the objects in the third result set.</typeparam>
	/// <typeparam name="TResultSet4">The type of the objects in the fourth result set.</typeparam>
	/// <typeparam name="TResultSet5">The type of the objects in the fifth result set.</typeparam>
	/// <param name="context">The database context to use for executing the stored procedure.</param>
	/// <param name="spParameters">
	///     The parameters required for the stored procedure, including the procedure name and SQL
	///     parameters.
	/// </param>
	/// <param name="resultSet1">
	///     The mapping function to convert rows in the first result set to objects of type
	///     <typeparamref name="TResultSet1" />.
	/// </param>
	/// <param name="resultSet2">
	///     The mapping function to convert rows in the second result set to objects of type
	///     <typeparamref name="TResultSet2" />.
	/// </param>
	/// <param name="resultSet3">
	///     The mapping function to convert rows in the third result set to objects of type
	///     <typeparamref name="TResultSet3" />.
	/// </param>
	/// <param name="resultSet4">
	///     The mapping function to convert rows in the fourth result set to objects of type
	///     <typeparamref name="TResultSet4" />.
	/// </param>
	/// <param name="resultSet5">
	///     The mapping function to convert rows in the fifth result set to objects of type
	///     <typeparamref name="TResultSet5" />.
	/// </param>
	/// <returns>
	///     A tuple containing five enumerable collections: the collections contain objects of type
	///     <typeparamref name="TResultSet1" />, <typeparamref name="TResultSet2" />,
	///     <typeparamref name="TResultSet3" />, <typeparamref name="TResultSet4" />,
	///     and <typeparamref name="TResultSet5" />, respectively.
	/// </returns>
	public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>,
            IEnumerable<TResultSet4>, IEnumerable<TResultSet5>)>
        QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>(
            this ICaeriusNetDbContext context,
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
        var results = await SqlCommandUtility.ExecuteMultipleIEnumerableResultSetsAsync(
            spParameters, context.DbConnection(),
            resultSet1, resultSet2, resultSet3, resultSet4, resultSet5);

        return (
            results[0].Cast<TResultSet1>(),
            results[1].Cast<TResultSet2>(),
            results[2].Cast<TResultSet3>(),
            results[3].Cast<TResultSet4>(),
            results[4].Cast<TResultSet5>());
    }
}