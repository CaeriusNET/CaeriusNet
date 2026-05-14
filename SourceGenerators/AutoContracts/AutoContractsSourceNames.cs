namespace CaeriusNet.Generator.AutoContracts;

internal static class AutoContractsSourceNames
{
    internal const string ManifestFileName = "caerius.contracts.json";

    internal static string BuildHintName(AutoContractsManifest manifest)
    {
        return HintNameBuilder.Build(
            manifest.Namespace,
            "CaeriusContracts",
            "AutoContracts." + BuildStablePathHash(manifest.SourcePath));
    }

    private static string BuildStablePathHash(string path)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            var hash = offset;

            for (var i = 0; i < path.Length; i++)
            {
                var ch = path[i] == '\\' ? '/' : path[i];
                hash ^= ch;
                hash *= prime;
            }

            return hash.ToString("x8");
        }
    }
}
