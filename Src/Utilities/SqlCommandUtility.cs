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

            command.Parameters.AddRange([..spParameters.Parameters]);
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
}