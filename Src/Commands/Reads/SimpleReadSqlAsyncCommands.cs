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
        if (TryRetrieveResultSetFromCache(spParameters, out TResultSet? cachedResult) && cachedResult != null)
            return cachedResult;

        try
        {
            var connection = context.DbConnection();
            using (connection)
            {
                await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
                await using var reader = await command.ExecuteReaderAsync();
                var result = await SqlCommandUtility.SingleResultSet<TResultSet>(reader);

                StoreResultSetInCache(spParameters, result);

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
        this ICaeriusDbContext context, StoredProcedureParameters spParameters)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        if (TryRetrieveResultSetFromCache(spParameters, out ReadOnlyCollection<TResultSet>? cachedResult) &&
            cachedResult != null) return cachedResult;

        try
        {
            var connection = context.DbConnection();
            using (connection)
            {
                await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
                await using var reader = await command.ExecuteReaderAsync();
                var results = await SqlCommandUtility.ResultsSets<TResultSet>(spParameters, reader);

                StoreResultSetInCache(spParameters, results.AsReadOnly());
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
        this ICaeriusDbContext context, StoredProcedureParameters spParameters)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        if (TryRetrieveResultSetFromCache(spParameters, out IEnumerable<TResultSet>? cachedResult) &&
            cachedResult != null) return cachedResult;

        try
        {
            var connection = context.DbConnection();
            using (connection)
            {
                await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
                await using var reader = await command.ExecuteReaderAsync();
                var results = await SqlCommandUtility.ResultsSets<TResultSet>(spParameters, reader);

                StoreResultSetInCache(spParameters, results.AsEnumerable());
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
        if (TryRetrieveResultSetFromCache(spParameters, out ImmutableArray<TResultSet> cachedResult) &&
            cachedResult != null)
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

                StoreResultSetInCache(spParameters, results.ToImmutable());
                return results.ToImmutable();
            }
        }
        catch (SqlException ex)
        {
            throw new CaeriusSqlException($"Failed to execute stored procedure : {spParameters.ProcedureName}", ex);
        }
    }

    private static bool TryRetrieveResultSetFromCache<TResultSet>(
        StoredProcedureParameters spParameters,
        out TResultSet? resultSet)
        where TResultSet : notnull
    {
        resultSet = default;

        if (string.IsNullOrEmpty(spParameters.CacheKey) || spParameters.CacheType == null)
            return false;

        switch (spParameters.CacheType)
        {
            case CacheType.InMemory:
                var cachedInMemory = Caching.InMemoryCacheManager.GetOrAdd(
                    spParameters.CacheKey,
                    () => new Dictionary<TResultSet, TResultSet>(),
                    spParameters.CacheExpiration!.Value);
                if (cachedInMemory.Count > 0)
                {
                    resultSet = cachedInMemory.Keys.First();
                    return true;
                }

                break;

            case CacheType.Frozen:
                var cachedFrozen = Caching.GetOrAdd(
                    spParameters.CacheKey,
                    () => new Dictionary<TResultSet, TResultSet>());
                if (cachedFrozen.Count > 0)
                {
                    resultSet = cachedFrozen.Keys.First();
                    return true;
                }

                break;
            case null:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return false;
    }

    private static void StoreResultSetInCache<TResultSet>(
        StoredProcedureParameters spParameters,
        TResultSet resultSet)
        where TResultSet : notnull
    {
        if (string.IsNullOrEmpty(spParameters.CacheKey) || spParameters.CacheType == null) return;

        switch (spParameters.CacheType)
        {
            case CacheType.InMemory:
                Caching.InMemoryCacheManager.GetOrAdd(
                    spParameters.CacheKey,
                    () => new Dictionary<TResultSet, TResultSet> { [resultSet] = resultSet },
                    spParameters.CacheExpiration!.Value);
                break;

            case CacheType.Frozen:
                Caching.GetOrAdd(
                    spParameters.CacheKey,
                    () => new Dictionary<TResultSet, TResultSet> { [resultSet] = resultSet });
                break;
            case null:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}