using System.IO;

namespace CaeriusNet.Analyzer.AutoContracts;

internal static class AutoContractsManifestFile
{
    internal const string ManifestFileName = "caerius.contracts.json";
    private const string ManifestMetadataName = "build_metadata.AdditionalFiles.CaeriusContractManifest";

    internal static bool IsManifest(AdditionalText additionalFile, AnalyzerConfigOptionsProvider optionsProvider)
    {
        var options = optionsProvider.GetOptions(additionalFile);
        if (options.TryGetValue(ManifestMetadataName, out var metadataValue))
            return string.Equals(metadataValue, "true", StringComparison.OrdinalIgnoreCase);

        return string.Equals(
            Path.GetFileName(additionalFile.Path),
            ManifestFileName,
            StringComparison.OrdinalIgnoreCase);
    }
}
