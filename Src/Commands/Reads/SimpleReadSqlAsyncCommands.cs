using CaeriusNet.Builders;
using CaeriusNet.Factories;
using CaeriusNet.Mappers;
using CaeriusNet.Utilities;

namespace CaeriusNet.Commands.Reads;

/// <summary>
///     Provides asynchronous TSQL query execution methods extending <see cref="ICaeriusDbConnectionFactory" />.
/// </summary>
public static class SimpleReadSqlAsyncCommands
{
    /// <summary>
    ///     Executes a TSQL query asynchronously and returns the first result as a specified type.
    /// </summary>
    /// <typeparam name="TResultSet">The type of the result set.</typeparam>
    /// <param name="connectionFactory">The database connection factory to create a connection.</param>
    /// <param name="spParameters">The stored procedure parameters builder containing the procedure name and parameters.</param>
    /// <returns>The first result of the query as the specified type mapped by <see cref="ISpMapper{T}" />.</returns>
    /// <exception cref="SqlException">Thrown when the query execution fails.</exception>
    public static async Task<TResultSet> FirstQueryAsync<TResultSet>(
        this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        try
        {
            var connection = connectionFactory.DbConnection();

            using (connection)
            {
                await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
                await using var reader = await command.ExecuteReaderAsync();
                var results = await SqlCommandUtility.SingleResultSet<TResultSet>(reader);
                return results;
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute query for stored procedure : {spParameters.ProcedureName} ;", ex);
        }
    }

    /// <summary>
    ///     Executes a TSQL query asynchronously and returns all results as a read-only collection of a specified type.
    /// </summary>
    /// <typeparam name="TResultSet">The type of the result set.</typeparam>
    /// <param name="connectionFactory">The database connection factory to create a connection.</param>
    /// <param name="spParameters">The stored procedure parameters builder containing the procedure name and parameters.</param>
    /// <returns>
    ///     A read-only collection of all results of the query as the specified type mapped by <see cref="ISpMapper{T}" />
    ///     .
    /// </returns>
    /// <exception cref="SqlException">Thrown when the query execution fails.</exception>
    public static async Task<ReadOnlyCollection<TResultSet>> QueryAsync<TResultSet>(
        this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        try
        {
            var connection = connectionFactory.DbConnection();

            using (connection)
            {
                await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
                await using var reader = await command.ExecuteReaderAsync();
                var results = await SqlCommandUtility.ResultsSets<TResultSet>(spParameters, reader);
                return results.AsReadOnly();
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute query for stored procedure : {spParameters.ProcedureName} ;", ex);
        }
    }

    /// <summary>
    ///     Executes a TSQL query asynchronously and returns all results as an enumerable of a specified type.
    /// </summary>
    /// <typeparam name="TResultSet">The type of the result set.</typeparam>
    /// <param name="connectionFactory">The database connection factory to create a connection.</param>
    /// <param name="spParameters">The stored procedure parameters builder containing the procedure name and parameters.</param>
    /// <returns>An enumerable of all results of the query as the specified type mapped by <see cref="ISpMapper{T}" />.</returns>
    /// <exception cref="SqlException">Thrown when the query execution fails.</exception>
    public static async Task<IEnumerable<TResultSet>> EnumerableQueryAsync<TResultSet>(
        this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        try
        {
            var connection = connectionFactory.DbConnection();

            using (connection)
            {
                await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
                await using var reader = await command.ExecuteReaderAsync();
                var results = await SqlCommandUtility.ResultsSets<TResultSet>(spParameters, reader);
                return results.AsEnumerable();
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute query for stored procedure : {spParameters.ProcedureName} ;", ex);
        }
    }

    /// <summary>
    ///     Executes a TSQL query asynchronously and returns all results as an immutable array of a specified type.
    /// </summary>
    /// <typeparam name="TResultSet">The type of the result set.</typeparam>
    /// <param name="connectionFactory">The database connection factory to create a connection.</param>
    /// <param name="spParameters">The stored procedure parameters builder containing the procedure name and parameters.</param>
    /// <returns>An immutable array of all results of the query as the specified type mapped by <see cref="ISpMapper{T}" />.</returns>
    /// <exception cref="SqlException">Thrown when the query execution fails.</exception>
    public static async Task<ImmutableArray<TResultSet>> ImmutableQueryAsync<TResultSet>(
        this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        try
        {
            var connection = connectionFactory.DbConnection();

            using (connection)
            {
                await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
                await using var reader = await command.ExecuteReaderAsync();

                var results = ImmutableArray.CreateBuilder<TResultSet>(spParameters.Capacity);

                while (await reader.ReadAsync())
                    results.AddRange(TResultSet.MapFromReader(reader));

                return results.ToImmutable();
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute query for stored procedure : {spParameters.ProcedureName} ;", ex);
        }
    }
}