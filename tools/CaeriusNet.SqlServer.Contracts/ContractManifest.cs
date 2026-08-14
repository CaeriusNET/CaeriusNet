namespace CaeriusNet.SqlServer.Contracts;

internal sealed record ContractManifest(
    int Version,
    string Namespace,
    DatabaseInfo Database,
    IReadOnlyList<TableTypeContract> TableTypes,
    IReadOnlyList<ProcedureContract> Procedures);