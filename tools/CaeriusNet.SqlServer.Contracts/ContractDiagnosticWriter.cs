namespace CaeriusNet.SqlServer.Contracts;

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