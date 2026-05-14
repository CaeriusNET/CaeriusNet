namespace CaeriusNet.Generator.AutoContracts;

/// <summary>
///     Generates SQL Server stored procedure contracts from the <c>caerius.contracts.json</c> manifest.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AutoContractsSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var manifests = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(static pair => AutoContractsManifestFile.IsManifest(pair.Left, pair.Right))
            .Select(static (pair, ct) => AutoContractsManifestParser.ParseOrDefault(pair.Left, ct))
            .Where(static manifest => manifest is not null)
            .Select(static (manifest, _) => manifest!);

        context.RegisterSourceOutput(manifests, static (spc, manifest) =>
        {
            if (!AutoContractsEmissionGuard.CanEmit(manifest))
                return;

            spc.AddSource(
                AutoContractsSourceNames.BuildHintName(manifest),
                AutoContractsEmitter.Emit(manifest));
        });
    }
}
