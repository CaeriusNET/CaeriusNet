using System.IO;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CaeriusNet.Generator.AutoContracts;

internal static class AutoContractsManifestFile
{
    private const string ManifestMetadataName = "build_metadata.AdditionalFiles.CaeriusContractManifest";

    internal static bool IsManifest(AdditionalText additionalFile, AnalyzerConfigOptionsProvider optionsProvider)
    {
        var options = optionsProvider.GetOptions(additionalFile);
        if (options.TryGetValue(ManifestMetadataName, out var metadataValue))
            return string.Equals(metadataValue, "true", StringComparison.OrdinalIgnoreCase);

        return string.Equals(
            Path.GetFileName(additionalFile.Path),
            AutoContractsSourceNames.ManifestFileName,
            StringComparison.OrdinalIgnoreCase);
    }
}
