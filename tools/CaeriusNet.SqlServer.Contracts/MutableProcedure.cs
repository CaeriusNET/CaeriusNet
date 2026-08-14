namespace CaeriusNet.SqlServer.Contracts;

internal sealed record MutableProcedure(ProcedureHeader Header)
{
    public List<ParameterContract> Parameters { get; } = [];
    public List<ColumnContract> FirstResultColumns { get; } = [];
    public string? ResultSetError { get; set; }
}