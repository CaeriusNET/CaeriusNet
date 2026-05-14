using System.Diagnostics;
using System.IO.Compression;
using System.Security;
using Xunit;

namespace CaeriusNet.Packaging.Tests;

public sealed class AutoContractsPackagingTests
{
    [Fact]
    public async Task PackageContainsBuildTransitiveImports()
    {
        var repoRoot = FindRepoRoot();
        var toolSource = Path.Combine(repoRoot, "Tools", "CaeriusNet.SqlServer.Contracts");

        using var temp = new TemporaryDirectory();
        var toolCopy = Path.Combine(temp.Path, "tool");
        var packageOutput = Path.Combine(temp.Path, "packages");
        CopyToolProject(toolSource, toolCopy);
        Directory.CreateDirectory(packageOutput);

        var result = await RunDotnetAsync(
            toolCopy,
            "pack",
            "CaeriusNet.SqlServer.Contracts.csproj",
            "--configuration",
            "Release",
            "--output",
            packageOutput);

        Assert.True(result.ExitCode == 0, result.ToString());

        var package = Assert.Single(Directory.EnumerateFiles(packageOutput, "CaeriusNet.SqlServer.Contracts.*.nupkg"));
        await using var archive = await ZipFile.OpenReadAsync(package);

        Assert.NotNull(archive.GetEntry("buildTransitive/CaeriusNet.SqlServer.Contracts.props"));
        Assert.NotNull(archive.GetEntry("buildTransitive/CaeriusNet.SqlServer.Contracts.targets"));
        Assert.NotNull(archive.GetEntry("tools/net10.0/any/CaeriusNet.SqlServer.Contracts.dll"));
        Assert.NotNull(archive.GetEntry("tools/net10.0/any/CaeriusNet.SqlServer.Contracts.deps.json"));
        Assert.NotNull(archive.GetEntry("tools/net10.0/any/CaeriusNet.SqlServer.Contracts.runtimeconfig.json"));

        var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        await using var stream = await nuspec.OpenAsync();
        using var reader = new StreamReader(stream);
        var nuspecText = await reader.ReadToEndAsync();
        Assert.DoesNotContain("DotnetTool", nuspecText, StringComparison.Ordinal);
        Assert.DoesNotContain("<packageTypes", nuspecText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PackageCanBeConsumedAsBuildTransitivePackageReference()
    {
        var repoRoot = FindRepoRoot();
        var toolSource = Path.Combine(repoRoot, "Tools", "CaeriusNet.SqlServer.Contracts");

        using var temp = new TemporaryDirectory();
        var toolCopy = Path.Combine(temp.Path, "tool");
        var packageOutput = Path.Combine(temp.Path, "packages");
        var consumerProject = Path.Combine(temp.Path, "consumer", "Consumer.csproj");
        var toolCommandOutput = Path.Combine(temp.Path, "consumer", "tool-command.txt");
        var packageCache = Path.Combine(temp.Path, "nuget-packages");
        CopyToolProject(toolSource, toolCopy);
        Directory.CreateDirectory(packageOutput);
        Directory.CreateDirectory(Path.GetDirectoryName(consumerProject)!);

        var packResult = await RunDotnetAsync(
            toolCopy,
            "pack",
            "CaeriusNet.SqlServer.Contracts.csproj",
            "--configuration",
            "Release",
            "--output",
            packageOutput);

        Assert.True(packResult.ExitCode == 0, packResult.ToString());

        await File.WriteAllTextAsync(
            consumerProject,
            $$"""
              <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                      <TargetFramework>net10.0</TargetFramework>
                      <CaeriusContractsMode>Off</CaeriusContractsMode>
                  </PropertyGroup>

                  <ItemGroup>
                      <PackageReference Include="CaeriusNet.SqlServer.Contracts"
                                        Version="11.0.3"
                                        PrivateAssets="all"/>
                  </ItemGroup>

                  <Target Name="WriteContractsToolCommand">
                      <WriteLinesToFile File="{{Escape(toolCommandOutput)}}"
                                        Lines="$(CaeriusContractsToolPath)|$(CaeriusContractsToolCommand)"
                                        Overwrite="true"/>
                  </Target>
              </Project>
              """);

        var buildResult = await RunDotnetAsync(
            Path.GetDirectoryName(consumerProject)!,
            new Dictionary<string, string>
            {
                ["NUGET_PACKAGES"] = packageCache
            },
            "build",
            consumerProject,
            "--configuration",
            "Release",
            "--source",
            packageOutput);

        Assert.True(buildResult.ExitCode == 0, buildResult.ToString());

        var printResult = await RunDotnetAsync(
            Path.GetDirectoryName(consumerProject)!,
            new Dictionary<string, string>
            {
                ["NUGET_PACKAGES"] = packageCache
            },
            "msbuild",
            consumerProject,
            "/t:WriteContractsToolCommand",
            "/p:RestoreSources=" + packageOutput,
            "/restore",
            "/nologo",
            "/v:minimal");

        Assert.True(printResult.ExitCode == 0, printResult.ToString());

        var line = Assert.Single(await File.ReadAllLinesAsync(toolCommandOutput));
        var parts = line.Split('|', 2);
        Assert.Equal(2, parts.Length);
        Assert.True(File.Exists(parts[0]), $"Expected packaged tool path to exist: {parts[0]}");
        Assert.Contains("CaeriusNet.SqlServer.Contracts.dll", parts[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task TargetsAddManifestCreatedDuringMsbuildExecution()
    {
        var repoRoot = FindRepoRoot();
        var props = Path.Combine(repoRoot, "Tools", "CaeriusNet.SqlServer.Contracts", "buildTransitive",
            "CaeriusNet.SqlServer.Contracts.props");
        var targets = Path.Combine(repoRoot, "Tools", "CaeriusNet.SqlServer.Contracts", "buildTransitive",
            "CaeriusNet.SqlServer.Contracts.targets");

        using var temp = new TemporaryDirectory();
        var project = Path.Combine(temp.Path, "Smoke.proj");
        var manifest = Path.Combine(temp.Path, "generated.contracts.json");
        var additionalFiles = Path.Combine(temp.Path, "additional-files.txt");

        await File.WriteAllTextAsync(
            project,
            $$"""
              <Project>
                  <PropertyGroup>
                      <CaeriusContractsOutput>{{Escape(manifest)}}</CaeriusContractsOutput>
                  </PropertyGroup>

                  <Import Project="{{Escape(props)}}"/>
                  <Import Project="{{Escape(targets)}}"/>

                  <Target Name="CreateManifest">
                      <WriteLinesToFile File="$(CaeriusContractsOutput)" Lines="{}" Overwrite="true"/>
                  </Target>

                  <Target Name="RunSmoke" DependsOnTargets="CreateManifest;CaeriusAddContractsManifest">
                      <WriteLinesToFile File="{{Escape(additionalFiles)}}"
                                        Lines="@(AdditionalFiles->'%(FullPath)|%(CaeriusContractManifest)')"
                                        Overwrite="true"/>
                  </Target>
              </Project>
              """);

        var result = await RunDotnetAsync(temp.Path, "msbuild", project, "/t:RunSmoke", "/nologo", "/v:minimal");

        Assert.True(result.ExitCode == 0, result.ToString());
        Assert.Contains(
            await File.ReadAllLinesAsync(additionalFiles),
            line => string.Equals(line, $"{Path.GetFullPath(manifest)}|true", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TargetsCanUseConnectionNameConfigurationArguments()
    {
        var repoRoot = FindRepoRoot();
        var props = Path.Combine(repoRoot, "Tools", "CaeriusNet.SqlServer.Contracts", "buildTransitive",
            "CaeriusNet.SqlServer.Contracts.props");
        var targets = Path.Combine(repoRoot, "Tools", "CaeriusNet.SqlServer.Contracts", "buildTransitive",
            "CaeriusNet.SqlServer.Contracts.targets");

        using var temp = new TemporaryDirectory();
        var project = Path.Combine(temp.Path, "Smoke.proj");
        var argumentsOutput = Path.Combine(temp.Path, "connection-arguments.txt");

        await File.WriteAllTextAsync(
            project,
            $$"""
              <Project>
                  <PropertyGroup>
                      <CaeriusContractsConnectionName>ApplicationDb</CaeriusContractsConnectionName>
                      <CaeriusContractsConfigurationEnvironment>Development</CaeriusContractsConfigurationEnvironment>
                      <UserSecretsId>caerius-test-secrets</UserSecretsId>
                  </PropertyGroup>

                  <Import Project="{{Escape(props)}}"/>
                  <Import Project="{{Escape(targets)}}"/>

                  <Target Name="RunSmoke">
                      <WriteLinesToFile File="{{Escape(argumentsOutput)}}"
                                        Lines="$(CaeriusContractsConnectionArguments)"
                                        Overwrite="true"/>
                  </Target>
              </Project>
              """);

        var result = await RunDotnetAsync(temp.Path, "msbuild", project, "/t:RunSmoke", "/nologo", "/v:minimal");

        Assert.True(result.ExitCode == 0, result.ToString());
        var line = Assert.Single(await File.ReadAllLinesAsync(argumentsOutput));
        Assert.Contains("--connection-name \"ApplicationDb\"", line, StringComparison.Ordinal);
        Assert.Contains("--configuration-base-path", line, StringComparison.Ordinal);
        Assert.Contains("--configuration-environment \"Development\"", line, StringComparison.Ordinal);
        Assert.Contains("--user-secrets-id \"caerius-test-secrets\"", line, StringComparison.Ordinal);
        Assert.DoesNotContain("--connection-env", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoContractsMsbuildFilesDoNotUseVersionedNames()
    {
        var repoRoot = FindRepoRoot();
        var buildTransitive = Path.Combine(repoRoot, "Tools", "CaeriusNet.SqlServer.Contracts", "buildTransitive");

        foreach (var file in Directory.EnumerateFiles(buildTransitive, "*", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);

            Assert.DoesNotContain("V" + "1", text, StringComparison.Ordinal);
            Assert.DoesNotContain("v" + "1", text, StringComparison.Ordinal);
        }
    }

    private static Task<CommandResult> RunDotnetAsync(string workingDirectory, params string[] arguments)
    {
        return RunDotnetAsync(workingDirectory, null, arguments);
    }

    private static async Task<CommandResult> RunDotnetAsync(
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environmentVariables,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        if (environmentVariables is not null)
            foreach (var (key, value) in environmentVariables)
                startInfo.Environment[key] = value;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new CommandResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CaeriusNet.slnx")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Tools", "CaeriusNet.SqlServer.Contracts")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static string Escape(string value)
    {
        return SecurityElement.Escape(value) ?? value;
    }

    private static void CopyToolProject(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(Path.Combine(destination, "buildTransitive"));

        File.Copy(
            Path.Combine(source, "CaeriusNet.SqlServer.Contracts.csproj"),
            Path.Combine(destination, "CaeriusNet.SqlServer.Contracts.csproj"));

        foreach (var file in Directory.EnumerateFiles(source, "*.cs"))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));

        foreach (var file in Directory.EnumerateFiles(source, "*.md"))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));

        foreach (var file in Directory.EnumerateFiles(Path.Combine(source, "buildTransitive")))
            File.Copy(file, Path.Combine(destination, "buildTransitive", Path.GetFileName(file)));
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
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "caerius-packaging-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }
}
