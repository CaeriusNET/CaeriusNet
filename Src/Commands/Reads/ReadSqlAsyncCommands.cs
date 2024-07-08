using CaeriusNet.Builders;
using CaeriusNet.Factories;
using CaeriusNet.Mappers;
using CaeriusNet.Utilities;

namespace CaeriusNet.Commands.Reads;

public static class ReadSqlAsyncCommands
{
    public static async Task<T> FirstQueryAsync<T>(
        this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
        where T : class, ISpMapper<T>
    {
        try
        {
            var connection = connectionFactory.DbConnection();

            using (connection)
            {
                await using var command = await SqlCommandUtility.CreateSqlCommand(spParameters, connection);
                await using var reader = await command.ExecuteReaderAsync();
                var results = await SqlCommandUtility.SingleResultSet<T>(reader);
                return results;
            }
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to execute query for stored procedure : {spParameters.ProcedureName} ;", ex);
        }
    }

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
                await using var command = await SqlCommandUtility.CreateSqlCommand(spParameters, connection);
                await using var reader = await command.ExecuteReaderAsync();
                var results = await SqlCommandUtility.ResultsSets<T>(spParameters, reader);
                return results.AsReadOnly();
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
                await using var command = await SqlCommandUtility.CreateSqlCommand(spParameters, connection);
                await using var reader = await command.ExecuteReaderAsync();
                var results = await SqlCommandUtility.ResultsSets<T>(spParameters, reader);
                return results.AsEnumerable();
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
                await using var command = await SqlCommandUtility.CreateSqlCommand(spParameters, connection);
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