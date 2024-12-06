using CaeriusNet.Builders;
using CaeriusNet.Factories;
using CaeriusNet.Utilities;

namespace CaeriusNet.Commands.Writes;

/// <summary>
///     Provides asynchronous TSQL command execution methods extending <see cref="ICaeriusDbContext" />.
/// </summary>
public static class WriteSqlAsyncCommands
{
    /// <summary>
    ///     Executes a TSQL command that returns a single value asynchronously.
    /// </summary>
    /// <param name="context">The database connection factory to create a connection.</param>
    /// <param name="spParameters">The stored procedure parameters builder containing the procedure name and parameters.</param>
    /// <exception cref="Exception">Throws an exception if the command execution fails.</exception>
    public static async Task<object?> ExecuteScalarAsync(this ICaeriusDbContext context,
        StoredProcedureParametersBuilder spParameters)
    {
        try
        {
            var connection = context.DbConnection();

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
    /// <param name="context">The database connection factory to create a connection.</param>
    /// <param name="spParameters">The stored procedure parameters builder containing the procedure name and parameters.</param>
    /// <returns>The number of rows affected.</returns>
    /// <exception cref="Exception">Throws an exception if the command execution fails.</exception>
    public static async Task<int> ExecuteAsync(this ICaeriusDbContext context,
        StoredProcedureParametersBuilder spParameters)
    {
        try
        {
            var connection = context.DbConnection();

            using (connection)
            {
                await using var command = await SqlCommandUtility.ExecuteSqlCommand(spParameters, connection);
                var resultRow = await command.ExecuteNonQueryAsync();
                return resultRow;
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute command for stored procedure; {spParameters.ProcedureName} ::", ex);
        }
    }
}