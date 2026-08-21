namespace CaeriusNet.Analyzer.AutoContracts;

internal sealed record AutoContractsManifest(
    string SourcePath,
    int Version,
    string Namespace,
    EquatableArray<AutoContractsTableType> TableTypes,
    EquatableArray<AutoContractsProcedure> Procedures,
    int ServerMajor = 0,
    EquatableArray<string> Features = default);

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
    EquatableArray<AutoContractsParameter> Parameters,
    EquatableArray<AutoContractsResultSet> ResultSets,
    string ContractHash);

internal sealed record AutoContractsParameter(
    int Ordinal,
    string Name,
    string SqlType,
    string ClrType,
    bool IsTableType,
    string Direction,
    bool Nullable,
    int? MaxLength,
    byte? Precision,
    byte? Scale);

internal sealed record AutoContractsResultSet(
    int Index,
    string? ClrName,
    string Status,
    EquatableArray<AutoContractsColumn> Columns,
    string? ErrorMessage,
    string ContractHash);

internal sealed record AutoContractsOverrides(
    string SourcePath,
    int Version,
    EquatableArray<AutoContractsProcedureOverride> Procedures);

internal sealed record AutoContractsProcedureOverride(
    string Schema,
    string Name,
    EquatableArray<AutoContractsResultSet> ResultSets,
    EquatableArray<AutoContractsParameterDirectionOverride> ParameterDirections);

/// <summary>
///     Explicitly changes the ADO.NET direction selected for a SQL output-capable parameter.
///     SQL Server catalog metadata only exposes <c>is_output</c>; it cannot distinguish an output-only
///     call from an input/output call.  The discovery tool therefore emits <c>Output</c> and this
///     opt-in override is the deliberate, execution-free way to request <c>InputOutput</c>.
/// </summary>
internal sealed record AutoContractsParameterDirectionOverride(int Ordinal, string Direction);

internal sealed record AutoContractsParseResult<T>(
    string SourcePath,
    T? Value,
    string? Error)
    where T : class
{
    internal bool IsSuccess => Value is not null && Error is null;
}

internal sealed record AutoContractsMergeResult(
    AutoContractsManifest Manifest,
    EquatableArray<AutoContractsGeneratorIssue> Issues);

internal sealed record AutoContractsGeneratorIssue(string SourcePath, string Id, string Message);

internal sealed record AutoContractsColumn(
    int Ordinal,
    string Name,
    string SqlType,
    string ClrType,
    bool Nullable,
    int? MaxLength,
    byte? Precision,
    byte? Scale,
    int? LocaleId = null,
    int? SqlCompareOptions = null,
    string? Collation = null);
