namespace CaeriusNet.SqlServer.Contracts;

internal enum ContractDiagnosticSeverity
{
    Warning,
    Error
}

internal sealed record ContractDiagnostic(
    string Id,
    ContractDiagnosticSeverity Severity,
    string Message);

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

internal static class ContractDiagnosticWriter
{
    internal static void Write(ContractDiagnosticSink sink)
    {
        foreach (var diagnostic in sink.Diagnostics)
        {
            var severity = diagnostic.Severity.ToString().ToLowerInvariant();
            Console.Error.WriteLine($"{diagnostic.Id} {severity}: {diagnostic.Message}");
        }
    }
}
