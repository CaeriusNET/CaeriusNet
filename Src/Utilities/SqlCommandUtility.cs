namespace CaeriusNet.Utilities;

/// <summary>
///     Provides utility methods for working with SqlCommand objects,
///     including executing stored procedures and handling result sets.
/// </summary>
public static class SqlCommandUtility
{
    /// <summary>
    ///     Executes a SQL stored procedure command using the provided parameters and database connection.
    /// </summary>
    /// <param name="spParameters">
    ///     An instance of <see cref="StoredProcedureParameters" /> containing the stored procedure's name and parameters.
    /// </param>
    /// <param name="connection">
    ///     An open database connection implementing <see cref="IDbConnection" />.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains the configured <see cref="SqlCommand" /> object.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="spParameters" /> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the associated connection does not support operations expected by <see cref="SqlCommand" />.
    /// </exception>
    /// <exception>
    ///     Any unhandled exceptions thrown during the execution of configuring or initializing the <see cref="SqlCommand" />.
    /// </exception>
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

    /// <summary>
    ///     Asynchronously retrieves a list of result sets mapped to a specified type from a <see cref="SqlDataReader" />,
    ///     based on the provided stored procedure parameters.
    /// </summary>
    /// <typeparam name="TResultSet">
    ///     The type of the result set that implements <see cref="ISpMapper{T}" /> for mapping data from the
    ///     <see cref="SqlDataReader" />.
    /// </typeparam>
    /// <param name="spParameters">
    ///     An instance of <see cref="StoredProcedureParameters" /> containing the stored procedure's details and parameters.
    /// </param>
    /// <param name="reader">
    ///     An active instance of <see cref="SqlDataReader" /> from which the result sets will be read and mapped.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation and returns a list of results of type
    ///     <typeparamref name="TResultSet" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="spParameters" /> or <paramref name="reader" /> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the mapping fails due to a discrepancy between the data reader fields and the expected result set
    ///     structure.
    /// </exception>
    /// <exception>
    ///     Any unhandled exceptions that may occur during data reading or mapping.
    /// </exception>
    public static async Task<List<TResultSet>> ResultsSets<TResultSet>(StoredProcedureParameters spParameters,
        SqlDataReader reader)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        var items = new List<TResultSet>(spParameters.Capacity);
        while (await reader.ReadAsync()) items.Add(TResultSet.MapFromDataReader(reader));
        return items;
    }

    /// <summary>
    ///     Retrieves a single result set from a <see cref="SqlDataReader" /> by mapping the data to the specified type.
    /// </summary>
    /// <typeparam name="TResultSet">
    ///     The type of the result set to map the data to. Must implement <see cref="ISpMapper{TResultSet}" />.
    /// </typeparam>
    /// <param name="reader">
    ///     The <see cref="SqlDataReader" /> instance containing the data to map.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains an instance of <typeparamref name="TResultSet" />
    ///     mapped from the data in the <paramref name="reader" />.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if there are multiple rows in the result that could be mapped, as only a single result is expected.
    /// </exception>
    /// <exception>
    ///     Any unhandled exceptions that occur during the mapping operation or reading from the <paramref name="reader" />.
    /// </exception>
    public static async Task<TResultSet> SingleResultSet<TResultSet>(SqlDataReader reader)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        var item = default(TResultSet)!;
        if (await reader.ReadAsync()) item = TResultSet.MapFromDataReader(reader);
        return item;
    }
}