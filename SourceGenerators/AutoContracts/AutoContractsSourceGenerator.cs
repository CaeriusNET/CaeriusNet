using System.IO;

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
            .Where(static text => string.Equals(
                Path.GetFileName(text.Path),
                AutoContractsSourceNames.ManifestFileName,
                StringComparison.OrdinalIgnoreCase))
            .Select(static (text, ct) => AutoContractsManifestParser.ParseOrDefault(text, ct))
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
