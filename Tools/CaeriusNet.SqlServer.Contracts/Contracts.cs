namespace CaeriusNet.SqlServer.Contracts;

internal sealed record ContractManifest(
    int Version,
    string Namespace,
    DatabaseInfo Database,
    IReadOnlyList<TableTypeContract> TableTypes,
    IReadOnlyList<ProcedureContract> Procedures);

internal sealed record DatabaseInfo(
    string Name,
    string ServerVersion,
    int CompatibilityLevel);

internal sealed record TableTypeContract(
    string Schema,
    string Name,
    string ClrName,
    IReadOnlyList<ColumnContract> Columns,
    string ContractHash);

internal sealed record ProcedureContract(
    string Schema,
    string Name,
    string ClrName,
    string ParametersClrName,
    string? ResultClrName,
    IReadOnlyList<ParameterContract> Parameters,
    ResultSetContract ResultSet,
    string ContractHash);

internal sealed record ParameterContract(
    int Ordinal,
    string Name,
    string SqlType,
    string ClrType,
    bool IsTableType,
    bool IsOutput,
    bool Nullable,
    int? MaxLength,
    byte? Precision,
    byte? Scale);

internal sealed record ResultSetContract(
    string Status,
    IReadOnlyList<ColumnContract> Columns,
    string? ErrorMessage);

internal sealed record ColumnContract(
    int Ordinal,
    string Name,
    string SqlType,
    string ClrType,
    bool Nullable,
    int? MaxLength,
    byte? Precision,
    byte? Scale);

internal sealed record MutableTableType(string Schema, string Name, int UserTypeId)
{
    public List<ColumnContract> Columns { get; } = [];
}

internal sealed record ProcedureHeader(int ObjectId, string Schema, string Name);