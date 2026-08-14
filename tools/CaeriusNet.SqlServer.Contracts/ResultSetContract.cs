namespace CaeriusNet.SqlServer.Contracts;

internal sealed record ResultSetContract(
    int Index,
    string? ClrName,
    string Status,
    IReadOnlyList<ColumnContract> Columns,
    string? ErrorMessage,
    string ContractHash);