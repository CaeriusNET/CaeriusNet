using System.IO.Compression;
using Xunit.Sdk;

namespace CaeriusNet.Packaging.Tests.Artifacts;

internal static class PackageArtifacts
{
    private static readonly string[] PackageIds =
        ["CaeriusNet", "CaeriusNet.Redis", "CaeriusNet.Aspire", "CaeriusNet.Contracts"];

    internal static IReadOnlyDictionary<string, string> RequireReleasePackages()
    {
        var artifactsDirectory = Path.Combine(FindRepositoryRoot(), "artifacts", "packages");
        if (!Directory.Exists(artifactsDirectory))
            throw SkipException.ForSkip(
                "Release packages are absent. Run 'dotnet pack --configuration Release --output artifacts/packages' first.");

        var packages = Directory.EnumerateFiles(artifactsDirectory, "*.nupkg", SearchOption.TopDirectoryOnly)
            .Where(static package => !package.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var packageId in PackageIds)
        {
            var packageFilePrefix = packageId + ".";
            var package = packages.SingleOrDefault(path =>
            {
                var fileName = Path.GetFileName(path);
                return fileName.StartsWith(packageFilePrefix, StringComparison.OrdinalIgnoreCase)
                       && fileName.Length > packageFilePrefix.Length
                       && char.IsAsciiDigit(fileName[packageFilePrefix.Length]);
            });
            if (package is null)
                throw SkipException.ForSkip(
                    $"Release package '{packageId}' is absent from {artifactsDirectory}.");
            result.Add(packageId, package);
        }

        return result;
    }

    internal static ZipArchive Open(string package)
    {
        return ZipFile.OpenRead(package);
    }

    internal static string ReadNuspec(string package)
    {
        using var archive = Open(package);
        var nuspec = archive.Entries.SingleOrDefault(static entry =>
                         entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidDataException($"Package '{package}' does not contain a nuspec.");
        using var stream = nuspec.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "CaeriusNet.slnx")))
                return directory.FullName;

        throw new DirectoryNotFoundException("Could not locate the CaeriusNet repository root.");
    }
}
