using System.Diagnostics;
using System.Security;
using CaeriusNet.Packaging.Tests.Artifacts;
using Xunit;
using Xunit.Sdk;

namespace CaeriusNet.Packaging.Tests.Contracts;

/// <summary>
///     Exercises the Contract package exactly as a downstream project does.  In particular, these tests
///     never add a project reference: the imported props, targets, and discovery executable all come from
///     the packed <c>CaeriusNet.Contracts</c> artifact.
/// </summary>
public sealed class ContractsPackageAcceptanceTests
{
    [Fact]
    public void BuildTransitiveFilesHaveSafeOptInAndSecretHandlingContracts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var props = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "CaeriusNet.Contracts",
            "Build",
            "CaeriusNet.Contracts.props"));
        var targets = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "CaeriusNet.Contracts",
            "Build",
            "CaeriusNet.Contracts.targets"));

        Assert.Contains("<CaeriusContractsMode Condition=\"'$(CaeriusContractsMode)' == ''\">Off", props,
            StringComparison.Ordinal);
        Assert.Contains("$(MSBuildThisFileDirectory)", props, StringComparison.Ordinal);
        Assert.Contains("../tools/net10.0/any/CaeriusNet.SqlServer.Contracts.dll", props, StringComparison.Ordinal);
        Assert.DoesNotContain("..\\tools\\net10.0", props, StringComparison.Ordinal);
        Assert.Contains("'$(CaeriusContractsMode.Trim().ToUpperInvariant())' == 'PULL'", targets,
            StringComparison.Ordinal);
        Assert.Contains("'$(CaeriusContractsMode.Trim().ToUpperInvariant())' == 'VERIFY'", targets,
            StringComparison.Ordinal);
        Assert.Contains("<Exec EchoOff=\"true\"", targets, StringComparison.Ordinal);
        Assert.Contains("--connection-env", targets, StringComparison.Ordinal);
        Assert.Contains("CaeriusContractsConnectionString is deprecated", targets, StringComparison.Ordinal);

        // The raw connection-string mode stays available only as the documented, deprecated escape hatch;
        // the target must not promote it to the recommended default path.
        Assert.Contains("--connection-name", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("<CaeriusContractsMode>Pull</CaeriusContractsMode>", props,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PackedContractsPackageIsAnOptInBuildTransitiveDependencyWithoutProjectReferences()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var package = RequireContractsPackage();
        var version = PackageVersion.FromPackagePath(package);

        using (var archive = PackageArtifacts.Open(package))
        {
            var propsEntry = archive.GetEntry("buildTransitive/CaeriusNet.Contracts.props");
            Assert.NotNull(propsEntry);
            Assert.NotNull(archive.GetEntry("buildTransitive/CaeriusNet.Contracts.targets"));
            Assert.NotNull(archive.GetEntry("tools/net10.0/any/CaeriusNet.SqlServer.Contracts.dll"));
            await using var propsStream = propsEntry!.Open();
            using var propsReader = new StreamReader(propsStream);
            var packedProps = await propsReader.ReadToEndAsync(cancellationToken);
            Assert.Contains("../tools/net10.0/any/CaeriusNet.SqlServer.Contracts.dll", packedProps,
                StringComparison.Ordinal);
            Assert.DoesNotContain("..\\tools\\net10.0", packedProps, StringComparison.Ordinal);
        }

        using var temporaryDirectory = new TemporaryDirectory();
        var consumerDirectory = Path.Combine(temporaryDirectory.Path, "consumer");
        Directory.CreateDirectory(consumerDirectory);

        await WriteNuGetConfigAsync(consumerDirectory, Path.GetDirectoryName(package)!, cancellationToken);

        var project = Path.Combine(consumerDirectory, "ContractsConsumer.csproj");
        var state = Path.Combine(consumerDirectory, "contracts-state.txt");
        var manifest = Path.Combine(consumerDirectory, "caerius.contracts.json");
        await File.WriteAllTextAsync(
            project,
            $$"""
              <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                  <TargetFramework>net10.0</TargetFramework>
                  <CaeriusContractsOutput>{{Escape(manifest)}}</CaeriusContractsOutput>
                </PropertyGroup>
                <ItemGroup>
                  <PackageReference Include="CaeriusNet.Contracts" Version="{{version}}" />
                </ItemGroup>
                <Target Name="CaptureCaeriusContractsState" AfterTargets="Build"
                        DependsOnTargets="CaeriusPrepareContracts">
                  <WriteLinesToFile File="{{Escape(state)}}"
                                    Lines="$(CaeriusContractsModeNormalized)|$(CaeriusContractsToolPath)|$(CaeriusContractsToolCommand)"
                                    Overwrite="true" />
                </Target>
              </Project>
              """,
            cancellationToken);

        var packageCache = Path.Combine(temporaryDirectory.Path, "nuget-cache");
        var build = await RunDotnetAsync(
            consumerDirectory,
            new Dictionary<string, string> { ["NUGET_PACKAGES"] = packageCache },
            cancellationToken,
            "build",
            project,
            "--configuration",
            "Release",
            "--nologo");

        Assert.True(build.ExitCode == 0, build.ToString());
        Assert.False(File.Exists(manifest), "The default Off mode must not invoke Pull or create a manifest.");

        var captured = Assert.Single(await File.ReadAllLinesAsync(state, cancellationToken));
        var fields = captured.Split('|', 3);
        Assert.Equal("OFF", fields[0]);
        Assert.Equal(3, fields.Length);
        Assert.True(File.Exists(fields[1]), $"The tool must be resolved from the package: {fields[1]}");
        Assert.Contains("CaeriusNet.SqlServer.Contracts.dll", fields[2], StringComparison.Ordinal);

        var projectAssets = await File.ReadAllTextAsync(
            Path.Combine(consumerDirectory, "obj", "project.assets.json"), cancellationToken);
        Assert.DoesNotContain("CaeriusNet.Contracts.csproj", projectAssets, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Src.Contracts", projectAssets, StringComparison.OrdinalIgnoreCase);
    }

    private static string RequireContractsPackage()
    {
        var packageDirectory = Environment.GetEnvironmentVariable("CAERIUS_PACKAGE_DIRECTORY");
        packageDirectory = string.IsNullOrWhiteSpace(packageDirectory)
            ? Path.Combine(FindRepositoryRoot(), "artifacts", "packages")
            : Path.GetFullPath(packageDirectory);
        if (!Directory.Exists(packageDirectory))
            throw SkipException.ForSkip(
                "Release packages are absent. Run 'dotnet pack --configuration Release --output artifacts/packages' first.");

        var package = Directory.EnumerateFiles(packageDirectory, "CaeriusNet.Contracts.*.nupkg")
            .SingleOrDefault(static path => !path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase));
        if (package is null)
            throw SkipException.ForSkip(
                "The CaeriusNet.Contracts release package is absent from artifacts/packages.");

        return package;
    }

    private static async Task WriteNuGetConfigAsync(
        string consumerDirectory,
        string packageDirectory,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(
            Path.Combine(consumerDirectory, "NuGet.config"),
            $$"""
              <?xml version="1.0" encoding="utf-8"?>
              <configuration>
                <packageSources>
                  <clear />
                  <add key="caerius-artifacts" value="{{Escape(Path.GetFullPath(packageDirectory))}}" />
                  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                </packageSources>
                <packageSourceMapping>
                  <packageSource key="caerius-artifacts">
                    <package pattern="CaeriusNet*" />
                  </packageSource>
                  <packageSource key="nuget.org">
                    <package pattern="*" />
                  </packageSource>
                </packageSourceMapping>
              </configuration>
              """,
            cancellationToken);
    }

    private static async Task<CommandResult> RunDotnetAsync(
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        if (environment is not null)
            foreach (var (name, value) in environment)
                startInfo.Environment[name] = value;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);
        return new CommandResult(process.ExitCode, await standardOutput, await standardError);
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

    private static string Escape(string value)
    {
        return SecurityElement.Escape(value) ?? value;
    }

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public override string ToString()
        {
            return $"""
                    Exit code: {ExitCode}
                    Standard output:
                    {StandardOutput}
                    Standard error:
                    {StandardError}
                    """;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "caerius-contracts-package-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }

    private static class PackageVersion
    {
        internal static string FromPackagePath(string packagePath)
        {
            const string prefix = "CaeriusNet.Contracts.";
            var name = Path.GetFileNameWithoutExtension(packagePath);
            Assert.StartsWith(prefix, name, StringComparison.Ordinal);
            return name[prefix.Length..];
        }
    }
}
