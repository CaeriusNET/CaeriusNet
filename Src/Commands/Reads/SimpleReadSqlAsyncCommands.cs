namespace CaeriusNet.Commands.Reads;

/// <summary>
///     Contains a set of asynchronous methods for executing TSQL queries to retrieve data,
///     supporting operations such as fetching single results, collections, or immutable arrays
///     of mapped stored procedure outcomes. Extends <see cref="ICaeriusNetDbContext" />.
/// </summary>
public static class SimpleReadSqlAsyncCommands
{
	/// <summary>
	///     Executes a stored procedure to query a single result asynchronously and optionally retrieves the result
	///     from cache if caching is enabled and the item is available. The result is mapped to a specified type
	///     that implements <see cref="ISpMapper{T}" />.
	/// </summary>
	/// <typeparam name="TResultSet">
	///     The type of the result set expected from the query. Must implement <see cref="ISpMapper{T}" />.
	/// </typeparam>
	/// <param name="context">
	///     An instance of <see cref="ICaeriusNetDbContext" /> representing the database context for establishing a connection.
	/// </param>
	/// <param name="spParameters">
	///     The parameters required to execute the stored procedure, including the procedure name,
	///     input parameters, and caching details.
	/// </param>
	/// <param name="cancellationToken">Token to cancel the operation.</param>
	/// <returns>
	///     Returns a task that represents the asynchronous operation. The task result is the mapped result of type
	///     <typeparamref name="TResultSet" /> if data is retrieved successfully; otherwise, returns null.
	/// </returns>
	/// <exception cref="CaeriusNetSqlException">
	///     Thrown when the execution of the stored procedure fails due to a SQL exception.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static async ValueTask<TResultSet?> FirstQueryAsync<TResultSet>(
		this ICaeriusNetDbContext context,
		StoredProcedureParameters spParameters,
		CancellationToken cancellationToken = default)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		if (spParameters.CacheType.HasValue && !string.IsNullOrEmpty(spParameters.CacheKey))
			if (CacheUtility.TryRetrieveFromCache<TResultSet>(spParameters, out var cachedResult))
				return cachedResult;

		try{
			using var connection = context.DbConnection();
			var result = await SqlCommandUtility.ScalarQueryAsync<TResultSet>(
			spParameters, connection, cancellationToken).ConfigureAwait(false);

			if (result is not null)
				CacheUtility.StoreInCache(spParameters, result);

			return result;
		}
		catch (SqlException ex){
			throw new CaeriusNetSqlException(
			$"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
		}
	}

	/// <summary>
	///     Executes a stored procedure and retrieves the result set as a read-only collection asynchronously,
	///     while optionally using caching if configured. The result is mapped to a specified type implementing
	///     <see cref="ISpMapper{T}" />.
	/// </summary>
	/// <typeparam name="TResultSet">
	///     The type of the elements in the returned read-only collection. Must implement <see cref="ISpMapper{T}" />.
	/// </typeparam>
	/// <param name="context">
	///     An instance of <see cref="ICaeriusNetDbContext" /> representing the database context for establishing a connection.
	/// </param>
	/// <param name="spParameters">
	///     The parameters required to execute the stored procedure, including procedure name, input parameters,
	///     caching details, and expiration policy.
	/// </param>
	/// <param name="cancellationToken">Token to cancel the operation.</param>
	/// <returns>
	///     Returns a task representing the asynchronous operation. The task result is a
	///     <see cref="System.Collections.ObjectModel.ReadOnlyCollection{T}" />
	///     containing the mapped results of type <typeparamref name="TResultSet" /> if the operation succeeds.
	/// </returns>
	/// <exception cref="CaeriusNetSqlException">
	///     Thrown when the execution of the stored procedure fails due to a SQL exception.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static async ValueTask<ReadOnlyCollection<TResultSet>> QueryAsReadOnlyCollectionAsync<TResultSet>(
		this ICaeriusNetDbContext context,
		StoredProcedureParameters spParameters,
		CancellationToken cancellationToken = default)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		if (CacheUtility.TryRetrieveFromCache(spParameters, out ReadOnlyCollection<TResultSet>? cachedResult) &&
		    cachedResult != null)
			return cachedResult;

		try{
			using var connection = context.DbConnection();
			var result = await SqlCommandUtility.ResultSetAsReadOnlyCollectionAsync<TResultSet>(
			spParameters, connection, cancellationToken).ConfigureAwait(false);

			if (result.Count == 0)
				result = EmptyCollections.ReadOnlyCollection<TResultSet>();

			CacheUtility.StoreInCache(spParameters, result);
			return result;
		}
		catch (SqlException ex){
			throw new CaeriusNetSqlException(
			$"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
		}
	}

	/// <summary>
	///     Executes a stored procedure asynchronously to retrieve a collection of mapped results. The results
	///     are returned as an <see cref="System.Collections.Generic.IEnumerable{T}" /> and can optionally be retrieved from
	///     cache if caching
	///     is enabled and the item is available.
	/// </summary>
	/// <typeparam name="TResultSet">
	///     The type of the result set expected from the query. Must implement <see cref="ISpMapper{T}" />.
	/// </typeparam>
	/// <param name="context">
	///     An instance of <see cref="ICaeriusNetDbContext" /> representing the database context for establishing a connection.
	/// </param>
	/// <param name="spParameters">
	///     The parameters required to execute the stored procedure, including the procedure name, input
	///     parameters, and caching details.
	/// </param>
	/// <param name="cancellationToken">Token to cancel the operation.</param>
	/// <returns>
	///     Returns a task that represents the asynchronous operation. The task result is an
	///     <see cref="System.Collections.Generic.IEnumerable{T}" />
	///     collection of mapped results of type <typeparamref name="TResultSet" /> if data is retrieved successfully.
	/// </returns>
	/// <exception cref="CaeriusNetSqlException">
	///     Thrown when the execution of the stored procedure fails due to a SQL exception.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static async ValueTask<IEnumerable<TResultSet>> QueryAsIEnumerableAsync<TResultSet>(
		this ICaeriusNetDbContext context,
		StoredProcedureParameters spParameters,
		CancellationToken cancellationToken = default)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		if (CacheUtility.TryRetrieveFromCache(spParameters, out IEnumerable<TResultSet>? cachedResult) &&
		    cachedResult != null)
			return cachedResult;

		try{
			var results = new List<TResultSet>(spParameters.Capacity);
			using var connection = context.DbConnection();

			await foreach (var item in SqlCommandUtility.StreamQueryAsync<TResultSet>(
			               spParameters, connection, cancellationToken).ConfigureAwait(false))
				results.Add(item);

			CacheUtility.StoreInCache(spParameters, results);

			return results;
		}
		catch (SqlException ex){
			throw new CaeriusNetSqlException(
			$"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
		}
	}

	/// <summary>
	///     Executes a stored procedure to retrieve a data set asynchronously and maps the result into an immutable array of a
	///     specified type. The result can optionally be retrieved from cache if caching is enabled and the item is available.
	/// </summary>
	/// <typeparam name="TResultSet">
	///     The type of each item in the resulting immutable array. Must implement <see cref="ISpMapper{T}" />.
	/// </typeparam>
	/// <param name="context">
	///     An instance of <see cref="ICaeriusNetDbContext" /> representing the database context used for establishing a
	///     connection.
	/// </param>
	/// <param name="spParameters">
	///     The parameters required for the execution of the stored procedure, including procedure name, input parameters,
	///     cache details, and capacity for expected results.
	/// </param>
	/// <param name="cancellationToken">Token to cancel the operation.</param>
	/// <returns>
	///     Returns a task representing the asynchronous operation. The task result is an
	///     <see cref="System.Collections.Immutable.ImmutableArray{T}" /> of
	///     type <typeparamref name="TResultSet" /> containing the mapped results if data is retrieved successfully.
	/// </returns>
	/// <exception cref="CaeriusNetSqlException">
	///     Thrown when the execution of the stored procedure fails due to a SQL exception.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public static async ValueTask<ImmutableArray<TResultSet>> QueryAsImmutableArrayAsync<TResultSet>(
		this ICaeriusNetDbContext context,
		StoredProcedureParameters spParameters,
		CancellationToken cancellationToken = default)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		if (CacheUtility.TryRetrieveFromCache(spParameters, out ImmutableArray<TResultSet>? cachedResult) &&
		    cachedResult.HasValue)
			return cachedResult.Value;

		try{
			using var connection = context.DbConnection();
			var result = await SqlCommandUtility.ResultSetAsImmutableArrayAsync<TResultSet>(
			spParameters, connection, cancellationToken).ConfigureAwait(false);

			CacheUtility.StoreInCache(spParameters, result);
			return result;
		}
		catch (SqlException ex){
			throw new CaeriusNetSqlException(
			$"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
		}
	}
}