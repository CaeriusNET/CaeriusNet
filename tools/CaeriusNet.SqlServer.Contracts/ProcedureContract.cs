namespace CaeriusNet.SqlServer.Contracts;

internal sealed record ProcedureContract(
    string Schema,
    string Name,
    string ClrName,
    string ParametersClrName,
    IReadOnlyList<ParameterContract> Parameters,
    IReadOnlyList<ResultSetContract> ResultSets,
    string ContractHash);