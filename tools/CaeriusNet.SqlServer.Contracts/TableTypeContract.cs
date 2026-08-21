namespace CaeriusNet.SqlServer.Contracts;

internal sealed record TableTypeContract(
    string Schema,
    string Name,
    string ClrName,
    IReadOnlyList<ColumnContract> Columns,
    string ContractHash);
