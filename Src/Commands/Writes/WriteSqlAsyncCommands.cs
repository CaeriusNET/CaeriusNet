using CaeriusNet.Builders;
using CaeriusNet.Factories;

namespace CaeriusNet.Commands.Writes;

public static class WriteSqlAsyncCommands
{
    public static async Task ExecuteScalarAsync(
        this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
    {
        try
        {
            var connection = connectionFactory.DbConnection();

            using (connection)
            {
                await using var command = new SqlCommand(spParameters.ProcedureName, connection as SqlConnection);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.AddRange([..spParameters.Parameters]);

                await command.ExecuteScalarAsync();
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute command for stored procedure {spParameters.ProcedureName}", ex);
        }
    }

    public static async Task<int> ExecuteAsync(
        this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
    {
        try
        {
            var connection = connectionFactory.DbConnection();

            using (connection)
            {
                await using var command = new SqlCommand(spParameters.ProcedureName, connection as SqlConnection);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.AddRange([..spParameters.Parameters]);

                return await command.ExecuteNonQueryAsync();
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute command for stored procedure {spParameters.ProcedureName}", ex);
        }
    }
}