namespace CaeriusNet.Generator.Tvp;

/// <summary>Descriptors reported directly by the manual TVP generator when it is run without the analyzer.</summary>
internal static class TvpGeneratorDiagnostics
{
    internal static DiagnosticDescriptor GetDescriptor(string diagnosticId)
    {
        return string.Equals(diagnosticId, "CAERIUS005", StringComparison.Ordinal)
            ? UnsupportedSqlMapping
            : InvalidManualTvpColumn;
    }
#pragma warning disable RS1007 // Diagnostics deliberately use the invariant English strings shipped by the analyzer.
    private const string Category = "CaeriusNet.Analyzer";

    private const string HelpLinkBase =
        "https://github.com/CaeriusNET/CaeriusNet/blob/main/docs/diagnostics/";

    internal static readonly DiagnosticDescriptor UnsupportedSqlMapping = new(
        "CAERIUS005",
        "Parameter type has no native SQL Server mapping",
        "Parameter '{0}' has no faithful SQL Server TVP mapping: {1}",
        Category,
        DiagnosticSeverity.Error,
        true,
        helpLinkUri: HelpLinkBase + "CAERIUS005.md",
        customTags: []);

    internal static readonly DiagnosticDescriptor InvalidManualTvpColumn = new(
        "CAERIUS007",
        "Manual TVP column metadata is invalid",
        "TVP parameter '{0}' has invalid SqlServerTvpColumn metadata: {1}",
        Category,
        DiagnosticSeverity.Error,
        true,
        helpLinkUri: HelpLinkBase + "CAERIUS007.md",
        customTags: []);

#pragma warning restore RS1007
}
