using System.IO.Compression;
using Xunit;

namespace CaeriusNet.Packaging.Tests.Artifacts;

public sealed class PackageArtifactTests
{
    [Fact]
    public void ReleaseArtifactsContainTheFourPackagesWithTheirRequiredPackageAssets()
    {
        var packages = PackageArtifacts.RequireReleasePackages();

        using var core = PackageArtifacts.Open(packages["CaeriusNet"]);
        AssertPackageReadmeIconAndLibrary(core, "CaeriusNet.dll");
        AssertEntry(core, "analyzers/dotnet/cs/CaeriusNet.Generator.dll");
        AssertEntry(core, "analyzers/dotnet/cs/CaeriusNet.Analyzer.dll");
        Assert.DoesNotContain(core.Entries,
            static entry => entry.FullName.StartsWith("buildTransitive/", StringComparison.Ordinal));

        using var redis = PackageArtifacts.Open(packages["CaeriusNet.Redis"]);
        AssertPackageReadmeIconAndLibrary(redis, "CaeriusNet.Redis.dll");
        Assert.DoesNotContain(redis.Entries,
            static entry => entry.FullName.StartsWith("buildTransitive/", StringComparison.Ordinal));

        using var aspire = PackageArtifacts.Open(packages["CaeriusNet.Aspire"]);
        AssertPackageReadmeIconAndLibrary(aspire, "CaeriusNet.Aspire.dll");
        Assert.DoesNotContain(aspire.Entries,
            static entry => entry.FullName.StartsWith("buildTransitive/", StringComparison.Ordinal));

        using var contracts = PackageArtifacts.Open(packages["CaeriusNet.Contracts"]);
        AssertPackageReadmeIconAndLibrary(contracts, "CaeriusNet.Contracts.dll");
        AssertEntry(contracts, "buildTransitive/CaeriusNet.Contracts.props");
        AssertEntry(contracts, "buildTransitive/CaeriusNet.Contracts.targets");
        AssertEntry(contracts, "tools/net10.0/any/CaeriusNet.SqlServer.Contracts.dll");
        AssertEntry(contracts, "tools/net10.0/any/CaeriusNet.SqlServer.Contracts.runtimeconfig.json");
    }

    [Fact]
    public void PackageDependenciesFollowThePublicFourPackageBoundaries()
    {
        var packages = PackageArtifacts.RequireReleasePackages();

        var coreNuspec = PackageArtifacts.ReadNuspec(packages["CaeriusNet"]);
        Assert.Contains("Microsoft.Data.SqlClient", coreNuspec, StringComparison.Ordinal);
        Assert.DoesNotContain("StackExchange.Redis", coreNuspec, StringComparison.Ordinal);
        Assert.DoesNotContain("Aspire.", coreNuspec, StringComparison.Ordinal);
        Assert.DoesNotContain("CaeriusNet.Contracts", coreNuspec, StringComparison.Ordinal);

        var redisNuspec = PackageArtifacts.ReadNuspec(packages["CaeriusNet.Redis"]);
        Assert.Contains("CaeriusNet", redisNuspec, StringComparison.Ordinal);
        Assert.Contains("StackExchange.Redis", redisNuspec, StringComparison.Ordinal);
        Assert.DoesNotContain("Aspire.", redisNuspec, StringComparison.Ordinal);

        var aspireNuspec = PackageArtifacts.ReadNuspec(packages["CaeriusNet.Aspire"]);
        Assert.Contains("CaeriusNet.Redis", aspireNuspec, StringComparison.Ordinal);
        Assert.Contains("Aspire.Microsoft.Data.SqlClient", aspireNuspec, StringComparison.Ordinal);
        Assert.Contains("Aspire.StackExchange.Redis", aspireNuspec, StringComparison.Ordinal);

        var contractsNuspec = PackageArtifacts.ReadNuspec(packages["CaeriusNet.Contracts"]);
        Assert.Contains("CaeriusNet", contractsNuspec, StringComparison.Ordinal);
        Assert.DoesNotContain("StackExchange.Redis", contractsNuspec, StringComparison.Ordinal);
        Assert.DoesNotContain("Aspire.", contractsNuspec, StringComparison.Ordinal);
    }

    private static void AssertPackageReadmeIconAndLibrary(ZipArchive archive, string assemblyName)
    {
        AssertEntry(archive, "README.md");
        AssertEntry(archive, "icon.png");
        AssertEntry(archive, $"lib/net10.0/{assemblyName}");
    }

    private static void AssertEntry(ZipArchive archive, string path)
    {
        Assert.NotNull(archive.GetEntry(path));
    }
}
