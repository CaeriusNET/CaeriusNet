using CaeriusNet.Analyzer.Roslyn;

namespace CaeriusNet.Generator.AutoContracts;

#pragma warning disable RS1007 // Generator diagnostics intentionally use invariant, secret-free text.
#pragma warning disable RS1015 // The package README is the public remediation guide.
#pragma warning disable RS1028 // These diagnostics do not require custom IDE filtering tags.

/// <summary>
///     Generates SQL Server stored procedure contracts from the <c>caerius.contracts.json</c> manifest.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AutoContractsSourceGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor InvalidManifest = new(
        "CAERIUSG001",
        "Invalid AutoContracts manifest",
        "AutoContracts manifest '{0}' is invalid: {1}",
        "CaeriusNet.AutoContracts",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidOverrides = new(
        "CAERIUSG002",
        "Invalid AutoContracts overrides",
        "AutoContracts overrides '{0}' are invalid: {1}",
        "CaeriusNet.AutoContracts",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidContract = new(
        "CAERIUSG003",
        "Invalid AutoContracts v2 contract",
        "AutoContracts contract '{0}' is invalid: {1}",
        "CaeriusNet.AutoContracts",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var manifestFiles = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(static pair => AutoContractsManifestFile.IsManifest(pair.Left, pair.Right));

        var parsedManifests = manifestFiles
            .Select(static (pair, ct) => AutoContractsManifestParser.ParseManifest(pair.Left, ct));

        context.RegisterSourceOutput(parsedManifests, static (spc, result) =>
        {
            if (!result.IsSuccess)
                spc.ReportDiagnostic(Diagnostic.Create(
                    InvalidManifest,
                    Location.None,
                    System.IO.Path.GetFileName(result.SourcePath),
                    result.Error ?? "The manifest could not be read."));
        });

        var manifests = parsedManifests
            .Where(static result => result.IsSuccess)
            .Select(static (result, _) => result.Value!);

        var overrideFiles = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(static pair => AutoContractsManifestFile.IsOverrides(pair.Left, pair.Right));

        var parsedOverrides = overrideFiles
            .Select(static (pair, ct) => AutoContractsManifestParser.ParseOverrides(pair.Left, ct));

        context.RegisterSourceOutput(parsedOverrides, static (spc, result) =>
        {
            if (!result.IsSuccess)
                spc.ReportDiagnostic(Diagnostic.Create(
                    InvalidOverrides,
                    Location.None,
                    System.IO.Path.GetFileName(result.SourcePath),
                    result.Error ?? "The overrides could not be read."));
        });

        var overrides = parsedOverrides
            .Where(static result => result.IsSuccess)
            .Select(static (result, _) => result.Value!)
            .Collect();

        var mergedManifests = manifests.Combine(overrides)
            .Select(static (pair, _) => AutoContractsManifestParser.Merge(
                pair.Left,
                new EquatableArray<AutoContractsOverrides>(pair.Right)));

        context.RegisterSourceOutput(mergedManifests, static (spc, manifest) =>
        {
            foreach (var issue in manifest.Issues)
                spc.ReportDiagnostic(Diagnostic.Create(
                    InvalidContract,
                    Location.None,
                    System.IO.Path.GetFileName(issue.SourcePath),
                    issue.Message));

            if (manifest.Issues.Count != 0 || !AutoContractsEmissionGuard.CanEmit(manifest.Manifest))
                return;

            spc.AddSource(
                AutoContractsSourceNames.BuildHintName(manifest.Manifest),
                AutoContractsEmitter.Emit(manifest.Manifest));
        });
    }
}

#pragma warning restore RS1028
#pragma warning restore RS1015
#pragma warning restore RS1007
