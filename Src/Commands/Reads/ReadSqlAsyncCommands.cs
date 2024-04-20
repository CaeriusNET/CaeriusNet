using CaeriusNet.Builders;
using CaeriusNet.Factories;
using CaeriusNet.Mappers;

namespace CaeriusNet.Commands.Reads;

public static class ReadSqlAsyncCommands
{
    public static async Task<ReadOnlyCollection<T>> QueryAsync<T>(
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

                var items = new List<T>(spParameters.Capacity);
                
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

    public static async Task<ImmutableArray<T>> ImmutableQueryAsync<T>(
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

    public static async Task<IEnumerable<T>> EnumerableQueryAsync<T>(
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
                
                 var results = new List<T>(spParameters.Capacity);

                while (await reader.ReadAsync())
                    results.Add(T.MapFromReader(reader));

                return results.AsEnumerable();
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute query for stored procedure : {spParameters.ProcedureName} ;", ex);
        }
    }

    public static async Task<ReadOnlyCollection<T>> FirstOrDefaultAsync<T>(
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

                var results = new List<T>(1);

                if (await reader.ReadAsync())
                    results.Add(T.MapFromReader(reader));

                return results.AsReadOnly();
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute query for stored procedure : {spParameters.ProcedureName} ;", ex);
        }
    }
}