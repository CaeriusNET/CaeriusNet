using System.Reflection;

namespace CaeriusNet.Analyzer.Tests;

public sealed class AutoContractsDiagnosticDescriptorTests
{
    private const string Category = "CaeriusNet.Analyzer";

    private const string HelpLinkBase =
        "https://github.com/CaeriusNET/CaeriusNet/blob/main/Documentations/diagnostics/";

    private static readonly IReadOnlyDictionary<string, ExpectedDiagnostic> ExpectedDiagnostics =
        new Dictionary<string, ExpectedDiagnostic>(StringComparer.Ordinal)
        {
            ["CAERIUS200"] = new(
                DiagnosticSeverity.Error,
                "Contract manifest is missing",
                "The CaeriusNet contract manifest '{0}' was not found"),
            ["CAERIUS201"] = new(
                DiagnosticSeverity.Error,
                "Procedure is missing from contract manifest",
                "Procedure '{0}' is missing from the CaeriusNet contract manifest"),
            ["CAERIUS202"] = new(
                DiagnosticSeverity.Error,
                "TVP is missing from contract manifest",
                "TVP '{0}' is referenced but is missing from the CaeriusNet contract manifest"),
            ["CAERIUS203"] = new(
                DiagnosticSeverity.Error,
                "TVP SQL type is not supported",
                "TVP column '{0}' uses unsupported SQL type '{1}'"),
            ["CAERIUS204"] = new(
                DiagnosticSeverity.Error,
                "First result set cannot be determined",
                "The first result set of procedure '{0}' cannot be determined by SQL Server"),
            ["CAERIUS205"] = new(
                DiagnosticSeverity.Warning,
                "Procedure has no result set",
                "Procedure '{0}' has no result set; no result DTO will be generated"),
            ["CAERIUS206"] = new(
                DiagnosticSeverity.Error,
                "Output parameter is not supported",
                "Procedure '{0}' has output parameter '{1}', which is not supported by generated contracts"),
            ["CAERIUS207"] = new(
                DiagnosticSeverity.Error,
                "SQL type is not mappable",
                "SQL type '{0}' cannot be mapped to generated C# code"),
            ["CAERIUS208"] = new(
                DiagnosticSeverity.Warning,
                "Nullable result column",
                "Result column '{0}' is nullable and will be emitted as a nullable CLR type"),
            ["CAERIUS209"] = new(
                DiagnosticSeverity.Error,
                "Contract hash mismatch",
                "Contract hash mismatch for '{0}'"),
            ["CAERIUS210"] = new(
                DiagnosticSeverity.Warning,
                "Procedure may be incompatible with generated contracts",
                "Procedure '{0}' may be incompatible with generated contracts: {1}")
        };

    [Fact]
    public void SupportedDiagnostics_Advertise_Only_Roslyn_Emitted_AutoContractsRules()
    {
        var supportedIds = new AutoContractsManifestAnalyzer()
            .SupportedDiagnostics
            .Select(diagnostic => diagnostic.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "CAERIUS200",
                "CAERIUS202",
                "CAERIUS203",
                "CAERIUS204",
                "CAERIUS205",
                "CAERIUS206",
                "CAERIUS207",
                "CAERIUS208",
                "CAERIUS210"
            ],
            supportedIds);
    }

    [Fact]
    public void AutoContractsDescriptors_MatchExpectedMetadata()
    {
        var descriptors = GetAnalyzerDescriptors();

        foreach (var (id, expected) in ExpectedDiagnostics)
        {
            var descriptor = Assert.Single(descriptors, descriptor => descriptor.Id == id);

            Assert.Equal(expected.Severity, descriptor.DefaultSeverity);
            Assert.Equal(expected.Title, descriptor.Title.ToString());
            Assert.Equal(expected.MessageFormat, descriptor.MessageFormat.ToString());
            Assert.Equal(Category, descriptor.Category);
            Assert.True(descriptor.IsEnabledByDefault);
            Assert.Equal(HelpLinkBase + id + ".md", descriptor.HelpLinkUri);
        }
    }

    [Fact]
    public void AutoContractsDescriptors_DoNotExposeVersionedNames()
    {
        var descriptors = GetAnalyzerDescriptors()
            .Where(descriptor => ExpectedDiagnostics.ContainsKey(descriptor.Id));

        foreach (var descriptor in descriptors)
        {
            var publicText = string.Join(
                Environment.NewLine,
                descriptor.Title,
                descriptor.MessageFormat,
                descriptor.Description);

            Assert.DoesNotContain("V" + "1", publicText, StringComparison.Ordinal);
            Assert.DoesNotContain("v" + "1", publicText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AnalyzerReleaseNotes_ContainAutoContractsRulesWithExpectedSeverities()
    {
        var releaseNotes = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "AnalyzerReleases.Unshipped.md"));
        var releaseRows = releaseNotes
            .Select(ParseReleaseRow)
            .Where(row => row is not null)
            .Select(row => row!.Value)
            .ToDictionary(row => row.Id, row => row.Severity, StringComparer.Ordinal);

        foreach (var (id, expected) in ExpectedDiagnostics)
            Assert.Equal(expected.Severity.ToString(), releaseRows[id]);
    }

    private static IReadOnlyList<DiagnosticDescriptor> GetAnalyzerDescriptors()
    {
        var descriptorsType = typeof(GeneratorUsageAnalyzer).Assembly.GetType(
            "CaeriusNet.Analyzer.Diagnostics.DiagnosticDescriptors",
            true)!;

        return descriptorsType
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(DiagnosticDescriptor))
            .Select(field => (DiagnosticDescriptor)field.GetValue(null)!)
            .ToArray();
    }

    private static (string Id, string Severity)? ParseReleaseRow(string line)
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length < 3 || !parts[0].StartsWith("CAERIUS", StringComparison.Ordinal))
            return null;

        return (parts[0], parts[2]);
    }

    private sealed record ExpectedDiagnostic(
        DiagnosticSeverity Severity,
        string Title,
        string MessageFormat);
}
