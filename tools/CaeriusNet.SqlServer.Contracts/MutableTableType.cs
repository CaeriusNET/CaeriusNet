namespace CaeriusNet.SqlServer.Contracts;

internal sealed record MutableTableType(string Schema, string Name, int UserTypeId)
{
    public List<ColumnContract> Columns { get; } = [];
}
