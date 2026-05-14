namespace CaeriusNet.Generator.AutoContracts;

internal static class AutoContractsSourceNames
{
    internal const string ManifestFileName = "caerius.contracts.json";

    internal static string BuildHintName(AutoContractsManifest manifest)
    {
        return HintNameBuilder.Build(manifest.Namespace, "CaeriusContracts", "AutoContracts");
    }
}