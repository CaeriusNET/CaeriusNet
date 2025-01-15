using CaeriusNet.Caches;

namespace CaeriusNet.Commands.Reads;

/// <summary>
///     Provides asynchronous TSQL query execution methods extending <see cref="ICaeriusDbContext" />.
/// </summary>
public static class SimpleReadSqlAsyncCommands
{
	/// <summary>
	///     Executes a TSQL query asynchronously and returns the first result as a specified type.
	/// </summary>
	/// <typeparam name="TResultSet">The type of the result set.</typeparam>
	/// <param name="context">The database connection factory to create a connection.</param>
	/// <param name="spParameters">The stored procedure parameters builder containing the procedure name and parameters.</param>
	/// <returns>The first result of the query as the specified type mapped by <see cref="ISpMapper{T}" />.</returns>
	/// <exception cref="CaeriusSqlException">Thrown when the query execution fails.</exception>
	public static async Task<TResultSet> FirstQueryAsync<TResultSet>(
		this ICaeriusDbContext context,
		StoredProcedureParameters spParameters)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		if (TryRetrieveFromCache(spParameters, out TResultSet? cachedResult) && cachedResult != null)
			return cachedResult;

		try
		{
			var connection = context.DbConnection();
			using (connection)
			{
				await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
				await using var reader = await command.ExecuteReaderAsync();
				var result = await SqlCommandUtility.SingleResultSet<TResultSet>(reader);

				StoreInCache(spParameters, result);

				return result;
			}
		}
		catch (SqlException ex)
		{
			throw new CaeriusSqlException($"Failed to execute stored procedure : {spParameters.ProcedureName}", ex);
		}
	}

	/// <summary>
	///     Executes a TSQL query asynchronously and returns all results as a read-only collection of a specified type.
	/// </summary>
	/// <typeparam name="TResultSet">The type of the result set.</typeparam>
	/// <param name="context">The database connection factory to create a connection.</param>
	/// <param name="spParameters">The stored procedure parameters builder containing the procedure name and parameters.</param>
	/// <returns>
	///     A read-only collection of all results of the query as the specified type mapped by <see cref="ISpMapper{T}" />.
	/// </returns>
	/// <exception cref="CaeriusSqlException">Thrown when the query execution fails.</exception>
	public static async Task<ReadOnlyCollection<TResultSet>> QueryAsync<TResultSet>(
		this ICaeriusDbContext context,
		StoredProcedureParameters spParameters)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		if (TryRetrieveFromCache(spParameters, out ReadOnlyCollection<TResultSet>? cachedResult) &&
		    cachedResult != null)
			return cachedResult;

		try
		{
			var connection = context.DbConnection();
			using (connection)
			{
				await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
				await using var reader = await command.ExecuteReaderAsync();
				var results = await SqlCommandUtility.ResultsSets<TResultSet>(spParameters, reader);

				StoreInCache(spParameters, results.AsReadOnly());

				return results.AsReadOnly();
			}
		}
		catch (SqlException ex)
		{
			throw new CaeriusSqlException($"Failed to execute stored procedure : {spParameters.ProcedureName}", ex);
		}
	}

	/// <summary>
	///     Executes a TSQL query asynchronously and returns all results as an enumerable of a specified type.
	/// </summary>
	/// <typeparam name="TResultSet">The type of the result set.</typeparam>
	/// <param name="context">The database connection factory to create a connection.</param>
	/// <param name="spParameters">The stored procedure parameters builder containing the procedure name and parameters.</param>
	/// <returns>An enumerable of all results of the query as the specified type mapped by <see cref="ISpMapper{T}" />.</returns>
	/// <exception cref="CaeriusSqlException">Thrown when the query execution fails.</exception>
	public static async Task<IEnumerable<TResultSet>> EnumerableQueryAsync<TResultSet>(
		this ICaeriusDbContext context,
		StoredProcedureParameters spParameters)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		if (TryRetrieveFromCache(spParameters, out IEnumerable<TResultSet>? cachedResult) && cachedResult != null)
			return cachedResult;

		try
		{
			var connection = context.DbConnection();
			using (connection)
			{
				await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
				await using var reader = await command.ExecuteReaderAsync();
				var results = await SqlCommandUtility.ResultsSets<TResultSet>(spParameters, reader);

				StoreInCache(spParameters, results.AsEnumerable());

				return results.AsEnumerable();
			}
		}
		catch (SqlException ex)
		{
			throw new CaeriusSqlException($"Failed to execute stored procedure : {spParameters.ProcedureName}", ex);
		}
	}

	/// <summary>
	///     Executes a TSQL query asynchronously and returns all results as an immutable array of a specified type.
	/// </summary>
	/// <typeparam name="TResultSet">The type of the result set.</typeparam>
	/// <param name="context">The database connection factory to create a connection.</param>
	/// <param name="spParameters">The stored procedure parameters builder containing the procedure name and parameters.</param>
	/// <returns>An immutable array of all results of the query as the specified type mapped by <see cref="ISpMapper{T}" />.</returns>
	/// <exception cref="CaeriusSqlException">Thrown when the query execution fails.</exception>
	public static async Task<ImmutableArray<TResultSet>> ImmutableQueryAsync<TResultSet>(
		this ICaeriusDbContext context,
		StoredProcedureParameters spParameters)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		if (TryRetrieveFromCache(spParameters, out ImmutableArray<TResultSet> cachedResult) && cachedResult != null)
			return cachedResult;

		try
		{
			var connection = context.DbConnection();
			using (connection)
			{
				await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
				await using var reader = await command.ExecuteReaderAsync();

				var results = ImmutableArray.CreateBuilder<TResultSet>(spParameters.Capacity);
				while (await reader.ReadAsync())
					results.AddRange(TResultSet.MapFromDataReader(reader));

				StoreInCache(spParameters, results.ToImmutable());

				return results.ToImmutable();
			}
		}
		catch (SqlException ex)
		{
			throw new CaeriusSqlException($"Failed to execute stored procedure : {spParameters.ProcedureName}", ex);
		}
	}

	/// <summary>
	///     Attempts to retrieve a cached result for the given stored procedure parameters.
	/// </summary>
	/// <typeparam name="T">The type of the cached value.</typeparam>
	/// <param name="spParameters">The parameters of the stored procedure, including cache configuration.</param>
	/// <param name="result">The output parameter where the cached result will be stored if found.</param>
	/// <returns>
	///     <c>true</c> if a cached result is successfully retrieved; otherwise, <c>false</c>.
	/// </returns>
	private static bool TryRetrieveFromCache<T>(
		StoredProcedureParameters spParameters,
		out T? result)
	{
		result = default;
		if (spParameters.CacheType is null || string.IsNullOrEmpty(spParameters.CacheKey))
			return false;

		return spParameters.CacheType switch
		{
			CacheType.InMemory => InMemoryCacheManager.TryGet(spParameters.CacheKey, out result),
			CacheType.Frozen => FrozenCacheManager.TryGetFrozen(spParameters.CacheKey, out result),
			_ => false
		};
	}

	/// <summary>
	///     Stores the specified result in a cache based on the provided stored procedure parameters.
	/// </summary>
	/// <typeparam name="T">The type of the result to be cached.</typeparam>
	/// <param name="spParameters">
	///     The stored procedure parameters containing cache key, cache type, and expiration
	///     information.
	/// </param>
	/// <param name="result">The result to be stored in the cache.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid cache type is specified in the parameters.</exception>
	private static void StoreInCache<T>(
		StoredProcedureParameters spParameters,
		T result)
	{
		if (spParameters.CacheType is null || string.IsNullOrEmpty(spParameters.CacheKey))
			return;

		switch (spParameters.CacheType)
		{
			case CacheType.InMemory:
				InMemoryCacheManager.Store(spParameters.CacheKey, result, spParameters.CacheExpiration!.Value);
				break;
			case CacheType.Frozen:
				FrozenCacheManager.StoreFrozen(spParameters.CacheKey, result);
				break;
			case null:
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}
}