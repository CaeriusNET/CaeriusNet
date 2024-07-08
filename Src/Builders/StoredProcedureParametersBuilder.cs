using CaeriusNet.Mappers;

namespace CaeriusNet.Builders;

public sealed record StoredProcedureParametersBuilder(string ProcedureName, int Capacity = 1)
{
    public List<SqlParameter> Parameters { get; } = [];

    public StoredProcedureParametersBuilder AddParameter(
        string storedProcedureName,
        object value,
        SqlDbType dbType)
    {
        Parameters.Add(new SqlParameter(storedProcedureName, dbType) { Value = value });

        return this;
    }

    public StoredProcedureParametersBuilder AddParameterAsTvp<T>(
        string parameterName,
        string tvpName,
        IEnumerable<T> items)
        where T : class, ITvpMapper<T>
    {
        var tvpMappers = items.ToList();
        var tvp = tvpMappers.FirstOrDefault() ??
                  throw new ArgumentException("No items found in the collection to map to a Table-Valued Parameter.");
        var dataTable = tvp.MapToDataTable(tvpMappers);
        SqlParameter parameter = new(parameterName, SqlDbType.Structured)
        {
            TypeName = tvpName,
            Value = dataTable
        };

        Parameters.Add(parameter);
        return this;
    }
}