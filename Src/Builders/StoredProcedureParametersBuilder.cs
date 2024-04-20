using CaeriusNet.Mappers;

namespace CaeriusNet.Builders;

public sealed record StoredProcedureParametersBuilder(string ProcedureName, int Capacity = 1000)
{
    public string ProcedureName { get; } = ProcedureName;
    public int Capacity { get; } = Capacity;
    public List<SqlParameter> Parameters { get; } = [];

    public StoredProcedureParametersBuilder AddStoredProcedureParameter(string name, object value, SqlDbType type)
    {
        Parameters.Add(new SqlParameter(name, type) { Value = value });
        return this;
    }

    public StoredProcedureParametersBuilder AddTableValuedParameter<T>(string parameterName, string tvpName,
        IEnumerable<T> items)
        where T : class, ITvpMapper<T>
    {
        var tvp = items.FirstOrDefault() ?? throw new ArgumentException("No items to map to Table-Valued Parameters");
        var dataTable = tvp.MapToDataTable(items);
        SqlParameter parameter = new(parameterName, SqlDbType.Structured)
        {
            TypeName = tvpName,
            Value = dataTable
        };

        Parameters.Add(parameter);
        return this;
    }
}