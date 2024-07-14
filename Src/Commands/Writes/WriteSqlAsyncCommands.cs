using CaeriusNet.Builders;
using CaeriusNet.Factories;
using CaeriusNet.Utilities;

namespace CaeriusNet.Commands.Writes;

/// <summary>
///     Provides asynchronous TSQL command execution methods extending <see cref="ICaeriusDbConnectionFactory" />.
/// </summary>
public static class WriteSqlAsyncCommands
{
    /// <summary>
    ///     Executes a TSQL command that returns a single value asynchronously.
    /// </summary>
    /// <param name="connectionFactory">The database connection factory to create a connection.</param>
    /// <param name="spParameters">The stored procedure parameters builder containing the procedure name and parameters.</param>
    /// <exception cref="Exception">Throws an exception if the command execution fails.</exception>
    public static async Task<object?> ExecuteScalarAsync(this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
    {
        try
        {
            var connection = connectionFactory.DbConnection();

            using (connection)
            {
                await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
                var resultSet = await command.ExecuteScalarAsync();
                return resultSet;
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute command for stored procedure; {spParameters.ProcedureName} ::", ex);
        }
    }

    /// <summary>
    ///     Executes a TSQL command that does not return a result set, but the number of rows affected, asynchronously.
    /// </summary>
    /// <param name="connectionFactory">The database connection factory to create a connection.</param>
    /// <param name="spParameters">The stored procedure parameters builder containing the procedure name and parameters.</param>
    /// <returns>The number of rows affected.</returns>
    /// <exception cref="Exception">Throws an exception if the command execution fails.</exception>
    public static async Task<int> ExecuteAsync(this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
    {
        try
        {
            var connection = connectionFactory.DbConnection();

            using (connection)
            {
                await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
                var rowResults = await command.ExecuteNonQueryAsync();
                return rowResults;
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute command for stored procedure; {spParameters.ProcedureName} ::", ex);
        }
    }
}