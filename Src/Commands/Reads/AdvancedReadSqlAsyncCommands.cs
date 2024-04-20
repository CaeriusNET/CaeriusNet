using CaeriusNet.Builders;
using CaeriusNet.Factories;
using CaeriusNet.Mappers;

namespace CaeriusNet.Commands.Reads;

public static class AdvancedReadSqlAsyncCommands
{
    public static async Task<ReadOnlyCollection<T>> AdvQueryAsync<T>(
        this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
        where T : class, ISpMapper<T>
    {
        try
        {
            var connection = connectionFactory.DbConnection();

            using (connection)
            {
                await using var command = new SqlCommand(spParameters.ProcedureName, connection as SqlConnection);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.AddRange([..spParameters.Parameters]);

                await using var reader = await command.ExecuteReaderAsync();

                await reader.ReadAsync();
                    var capacity = reader.GetInt32(0);
                
                await reader.NextResultAsync();
                
                var items = new List<T>(capacity);
                
                while (await reader.ReadAsync())
                    items.Add(T.MapFromReader(reader));

                var result = items.AsReadOnly();
                return result;
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute query for stored procedure : {spParameters.ProcedureName} ;", ex);
        }
    }

    public static async Task<ImmutableArray<T>> AdvImmutableQueryAsync<T>(
        this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
        where T : class, ISpMapper<T>
    {
        try
        {
            var connection = connectionFactory.DbConnection();

            using (connection)
            {
                await using var command = new SqlCommand(spParameters.ProcedureName, connection as SqlConnection);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.AddRange([..spParameters.Parameters]);
                
                await using var reader = await command.ExecuteReaderAsync();
                
                var results = ImmutableArray.CreateBuilder<T>(spParameters.Capacity);

                while (await reader.ReadAsync())
                    results.AddRange(T.MapFromReader(reader));

                return results.ToImmutable();
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute query for stored procedure : {spParameters.ProcedureName} ;", ex);
        }
    }
}