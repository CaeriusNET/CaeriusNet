using CaeriusNet.Builders;
using CaeriusNet.Mappers;

namespace CaeriusNet.Utilities;

public static class SqlCommandUtility
{
    public static async Task<SqlCommand> CreateSqlCommand(StoredProcedureParametersBuilder spParameters,
        IDbConnection connection)
    {
        SqlCommand? command = null;
        try
        {
            command = new SqlCommand(spParameters.ProcedureName, connection as SqlConnection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddRange([..spParameters.Parameters.ToArray()]);
            return command;
        }
        catch
        {
            if (command != null) await command.DisposeAsync();
            throw;
        }
    }

    public static async Task<List<T>> ResultsSets<T>(StoredProcedureParametersBuilder spParameters,
        SqlDataReader reader)
        where T : class, ISpMapper<T>
    {
        var items = new List<T>(spParameters.Capacity);

        while (await reader.ReadAsync())
            items.Add(T.MapFromReader(reader));

        return items;
    }

    public static async Task<T> SingleResultSet<T>(SqlDataReader reader)
        where T : class, ISpMapper<T>
    {
        var item = default(T)!;

        if (await reader.ReadAsync())
            item = T.MapFromReader(reader);

        return item;
    }

    public static async Task<object[]> MultipleQueryAsync(IDbConnection connection,
        StoredProcedureParametersBuilder spParameters)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        if (spParameters == null) throw new ArgumentNullException(nameof(spParameters));

        await using var command = await CreateSqlCommand(spParameters, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var results = await ReadMultipleResultSetsAsync(spParameters, reader);
        return results;
    }

    private static async Task<object[]> ReadMultipleResultSetsAsync(StoredProcedureParametersBuilder spParameters,
        SqlDataReader reader)
    {
        var results = new object[spParameters.Mappers.Count];

        for (var i = 0; i < spParameters.Mappers.Count; i++)
        {
            var listType = typeof(List<>).MakeGenericType(spParameters.Mappers[i].Method.ReturnType);
            var list = Activator.CreateInstance(listType);

            while (await reader.ReadAsync())
            {
                var item = spParameters.Mappers[i].DynamicInvoke(reader);
                listType.GetMethod("Add")?.Invoke(list, [item]);
            }

            results[i] = list!;

            if (!await reader.NextResultAsync()) break;
        }

        return results;
    }
}