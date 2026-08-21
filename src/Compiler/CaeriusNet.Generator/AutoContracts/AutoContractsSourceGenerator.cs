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
        true);

    private static readonly DiagnosticDescriptor InvalidOverrides = new(
        "CAERIUSG002",
        "Invalid AutoContracts overrides",
        "AutoContracts overrides '{0}' are invalid: {1}",
        "CaeriusNet.AutoContracts",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidContract = new(
        "CAERIUSG003",
        "Invalid AutoContracts v3 contract",
        "AutoContracts contract '{0}' is invalid: {1}",
        "CaeriusNet.AutoContracts",
        DiagnosticSeverity.Error,
        true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // The build-transitive Contracts props marks this property compiler-visible.  Keep the value
        // as a scalar in the incremental graph so toggling the opt-in invalidates only this generator.
        var sqlServer2025TypesEnabled = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) => AutoContractsSqlServer2025.IsEnabled(options.GlobalOptions));

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
                    Path.GetFileName(result.SourcePath),
                    result.Error ?? "The manifest could not be read."));
        });

        var manifestCollection = parsedManifests
            .Where(static result => result.IsSuccess)
            .Select(static (result, _) => result.Value!)
            .Collect();

        context.RegisterSourceOutput(manifestCollection, static (spc, manifests) =>
        {
            if (manifests.Length <= 1)
                return;
            spc.ReportDiagnostic(Diagnostic.Create(
                InvalidContract,
                Location.None,
                AutoContractsManifestFile.ManifestFileName,
                "Only one AutoContracts manifest AdditionalFile is allowed per compilation."));
        });

        var manifests = manifestCollection
            .Select(static (values, _) => values.Length == 1 ? values[0] : null);

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
                    Path.GetFileName(result.SourcePath),
                    result.Error ?? "The overrides could not be read."));
        });

        var overrides = parsedOverrides
            .Where(static result => result.IsSuccess)
            .Select(static (result, _) => result.Value!)
            .Collect();

        var mergedManifests = manifests.Combine(overrides).Combine(sqlServer2025TypesEnabled)
            .Select(static (pair, _) => (
                Manifest: pair.Left.Left is null
                    ? null
                    : AutoContractsManifestParser.Merge(
                        pair.Left.Left,
                        new EquatableArray<AutoContractsOverrides>(pair.Left.Right),
                        pair.Right),
                SqlServer2025TypesEnabled: pair.Right));

        context.RegisterSourceOutput(mergedManifests, static (spc, configured) =>
        {
            var manifest = configured.Manifest;
            if (manifest is null)
                return;
            foreach (var issue in manifest.Issues)
                spc.ReportDiagnostic(Diagnostic.Create(
                    InvalidContract,
                    Location.None,
                    Path.GetFileName(issue.SourcePath),
                    issue.Message));

            var sqlServer2025TypesEnabled = configured.SqlServer2025TypesEnabled;
            if (manifest.Issues.Count != 0 ||
                !AutoContractsEmissionGuard.CanEmit(manifest.Manifest, sqlServer2025TypesEnabled))
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
