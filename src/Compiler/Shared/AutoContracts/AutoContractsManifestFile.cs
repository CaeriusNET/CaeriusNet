namespace CaeriusNet.Analyzer.AutoContracts;

internal static class AutoContractsManifestFile
{
    internal const string ManifestFileName = "caerius.contracts.json";
    internal const string OverridesFileName = "caerius.contracts.overrides.json";
    private const string ManifestMetadataName = "build_metadata.AdditionalFiles.CaeriusContractManifest";
    private const string OverridesMetadataName = "build_metadata.AdditionalFiles.CaeriusContractOverrides";

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

    internal static bool IsOverrides(AdditionalText additionalFile, AnalyzerConfigOptionsProvider optionsProvider)
    {
        var options = optionsProvider.GetOptions(additionalFile);
        if (options.TryGetValue(OverridesMetadataName, out var metadataValue))
            return string.Equals(metadataValue, "true", StringComparison.OrdinalIgnoreCase);

        return string.Equals(
            Path.GetFileName(additionalFile.Path),
            OverridesFileName,
            StringComparison.OrdinalIgnoreCase);
    }
}
