namespace CaeriusNet.SqlServer.Contracts;

internal sealed record ContractDiagnostic(
    string Id,
    ContractDiagnosticSeverity Severity,
    string Message);