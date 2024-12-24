namespace CaeriusNet.Utilities;

public static class SqlCommandUtility
{
    public static async Task<SqlCommand> ExecuteSqlCommand(StoredProcedureParameters spParameters,
        IDbConnection connection)
    {
        SqlCommand? command = null;
        try
        {
            command = new SqlCommand(spParameters.ProcedureName, connection as SqlConnection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.AddRange([..spParameters.Parameters]);
            return command;
        }
        catch
        {
            if (command != null) await command.DisposeAsync();
            throw;
        }
    }

    public static async Task<List<TResultSet>> ResultsSets<TResultSet>(StoredProcedureParameters spParameters,
        SqlDataReader reader)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        var items = new List<TResultSet>(spParameters.Capacity);
        while (await reader.ReadAsync()) items.Add(TResultSet.MapFromDataReader(reader));
        return items;
    }

    public static async Task<TResultSet> SingleResultSet<TResultSet>(SqlDataReader reader)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        var item = default(TResultSet)!;
        if (await reader.ReadAsync()) item = TResultSet.MapFromDataReader(reader);
        return item;
    }
}