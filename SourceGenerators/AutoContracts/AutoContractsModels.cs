namespace CaeriusNet.Generator.AutoContracts;

internal sealed record AutoContractsManifest(
    string SourcePath,
    int Version,
    string Namespace,
    EquatableArray<AutoContractsTableType> TableTypes,
    EquatableArray<AutoContractsProcedure> Procedures);

internal sealed record AutoContractsTableType(
    string Schema,
    string Name,
    string ClrName,
    EquatableArray<AutoContractsColumn> Columns,
    string ContractHash);

internal sealed record AutoContractsProcedure(
    string Schema,
    string Name,
    string ClrName,
    string ParametersClrName,
    string? ResultClrName,
    EquatableArray<AutoContractsParameter> Parameters,
    AutoContractsResultSet ResultSet,
    string ContractHash);

internal sealed record AutoContractsParameter(
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

internal sealed record AutoContractsResultSet(
    string Status,
    EquatableArray<AutoContractsColumn> Columns,
    string? ErrorMessage);

internal sealed record AutoContractsColumn(
    int Ordinal,
    string Name,
    string SqlType,
    string ClrType,
    bool Nullable,
    int? MaxLength,
    byte? Precision,
    byte? Scale);
