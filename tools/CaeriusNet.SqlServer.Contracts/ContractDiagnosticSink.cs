namespace CaeriusNet.SqlServer.Contracts;

internal sealed class ContractDiagnosticSink
{
    private readonly List<ContractDiagnostic> _diagnostics = [];

    internal bool HasErrors =>
        _diagnostics.Exists(diagnostic => diagnostic.Severity == ContractDiagnosticSeverity.Error);

    internal IReadOnlyList<ContractDiagnostic> Diagnostics => _diagnostics;

    internal void Error(string id, string message)
    {
        _diagnostics.Add(new ContractDiagnostic(id, ContractDiagnosticSeverity.Error, message));
    }

    internal void Warning(string id, string message)
    {
        _diagnostics.Add(new ContractDiagnostic(id, ContractDiagnosticSeverity.Warning, message));
    }
}
