using CaeriusNet.Mappers;

namespace CaeriusNet.Builders;

/// <summary>
///     Builds the parameters for a stored procedure call, including support for Table-Valued Parameters (TVPs).
/// </summary>
public sealed record StoredProcedureParametersBuilder(string ProcedureName, int Capacity = 1)
{
    /// <summary>
    ///     Gets the list of TSQL parameters to be used in the stored procedure call.
    /// </summary>
    public List<SqlParameter> Parameters { get; } = [];

    /// <summary>
    ///     Adds a parameter to the stored procedure call.
    /// </summary>
    /// <param name="parameter">The name of the parameter.</param>
    /// <param name="value">The value of the parameter.</param>
    /// <param name="dbType">The TSQL data type of the parameter. Use <see cref="SqlDbType" /> enumeration.</param>
    /// <returns>The <see cref="StoredProcedureParametersBuilder" /> instance for chaining.</returns>
    public StoredProcedureParametersBuilder AddParameter(
        string parameter,
        object value,
        SqlDbType dbType)
    {
        Parameters.Add(new SqlParameter(parameter, dbType) { Value = value });
        return this;
    }

    /// <summary>
    ///     Adds a Table-Valued Parameter (TVP) to the stored procedure call.
    /// </summary>
    /// <typeparam name="T">The type of the object that maps to the TVP.</typeparam>
    /// <param name="parameterName">The name of the TVP parameter.</param>
    /// <param name="tvpName">The name of the TVP type in SQL Server.</param>
    /// <param name="items">The collection of items to map to the TVP using the <see cref="ITvpMapper{T}" /> interface.</param>
    /// <returns>The <see cref="StoredProcedureParametersBuilder" /> instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the items collection is empty.</exception>
    public StoredProcedureParametersBuilder AddParameterAsTvp<T>(
        string parameterName,
        string tvpName,
        IEnumerable<T> items)
        where T : class, ITvpMapper<T>
    {
        var tvpMappers = items.ToList();
        if (tvpMappers.Count == 0)
            throw new ArgumentException("No items found in the collection to map to a Table-Valued Parameter.");
        var dataTable = tvpMappers[0].MapToDataTable(tvpMappers);
        var parameter = new SqlParameter(parameterName, SqlDbType.Structured)
        {
            TypeName = tvpName,
            Value = dataTable
        };

        Parameters.Add(parameter);
        return this;
    }
}