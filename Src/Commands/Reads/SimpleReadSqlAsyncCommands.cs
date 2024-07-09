using CaeriusNet.Builders;
using CaeriusNet.Factories;
using CaeriusNet.Mappers;
using CaeriusNet.Utilities;

namespace CaeriusNet.Commands.Reads;

public static class SimpleReadSqlAsyncCommands
{
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
                await using var command = await SqlCommandUtility.CreateSqlCommand(spParameters, connection);
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
                await using var command = await SqlCommandUtility.CreateSqlCommand(spParameters, connection);
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
                await using var command = await SqlCommandUtility.CreateSqlCommand(spParameters, connection);
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
                await using var command = await SqlCommandUtility.CreateSqlCommand(spParameters, connection);
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