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

        using var temp = new TemporaryDirectory();
        var packageOutput = Path.Combine(temp.Path, "packages");
        var (package, _) = await PackMainPackageAsync(repoRoot, packageOutput);

        await using var archive = await ZipFile.OpenReadAsync(package);

        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains('\\', StringComparison.Ordinal));
        Assert.NotNull(archive.GetEntry("CaeriusNet.nuspec"));
        Assert.NotNull(archive.GetEntry("buildTransitive/CaeriusNet.props"));
        Assert.NotNull(archive.GetEntry("buildTransitive/CaeriusNet.targets"));
        Assert.NotNull(archive.GetEntry("tools/net10.0/any/CaeriusNet.SqlServer.Contracts.dll"));
        Assert.NotNull(archive.GetEntry("tools/net10.0/any/CaeriusNet.SqlServer.Contracts.deps.json"));
        Assert.NotNull(archive.GetEntry("tools/net10.0/any/CaeriusNet.SqlServer.Contracts.runtimeconfig.json"));
        Assert.NotNull(archive.GetEntry("tools/net10.0/any/Microsoft.Data.SqlClient.dll"));
        Assert.NotNull(
            archive.GetEntry("tools/net10.0/any/Microsoft.Extensions.Configuration.EnvironmentVariables.dll"));
        Assert.NotNull(archive.GetEntry("tools/net10.0/any/Microsoft.Extensions.Configuration.Json.dll"));

        var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        await using var stream = await nuspec.OpenAsync();
        using var reader = new StreamReader(stream);
        var nuspecText = await reader.ReadToEndAsync();
        Assert.Contains("<id>CaeriusNet</id>", nuspecText, StringComparison.Ordinal);
        Assert.DoesNotContain("<id>CaeriusNet.SqlServer.Contracts</id>", nuspecText, StringComparison.Ordinal);
        Assert.DoesNotContain("DotnetTool", nuspecText, StringComparison.Ordinal);
        Assert.DoesNotContain("<packageTypes", nuspecText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PackageCanBeConsumedAsBuildTransitivePackageReference()
    {
        var repoRoot = FindRepoRoot();

        using var temp = new TemporaryDirectory();
        var packageOutput = Path.Combine(temp.Path, "packages");
        var consumerProject = Path.Combine(temp.Path, "consumer", "Consumer.csproj");
        var toolCommandOutput = Path.Combine(temp.Path, "consumer", "tool-command.txt");
        var packageCache = Path.Combine(temp.Path, "nuget-packages");
        Directory.CreateDirectory(Path.GetDirectoryName(consumerProject)!);

        var (_, packageVersion) = await PackMainPackageAsync(repoRoot, packageOutput, packageCache);
        await WriteNuGetConfigAsync(Path.GetDirectoryName(consumerProject)!, packageOutput);

        await File.WriteAllTextAsync(
            consumerProject,
            $$"""
              <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                      <TargetFramework>net10.0</TargetFramework>
                      <CaeriusContractsMode>Off</CaeriusContractsMode>
                  </PropertyGroup>

                  <ItemGroup>
                      <PackageReference Include="CaeriusNet"
                                        Version="{{packageVersion}}"/>
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
            "Release");

        Assert.True(buildResult.ExitCode == 0, buildResult.ToString());

        var printResult = await RunDotnetAsync(
            Path.GetDirectoryName(consumerProject)!,
            new Dictionary<string, string>
            {
                ["NUGET_PACKAGES"] = packageCache
            },
            "msbuild",
            consumerProject,
            "/restore",
            "/t:WriteContractsToolCommand",
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
    public async Task PackageReferencePullHonorsProjectDefinedModeAndOutput()
    {
        var repoRoot = FindRepoRoot();

        using var temp = new TemporaryDirectory();
        var packageOutput = Path.Combine(temp.Path, "packages");
        var consumerDirectory = Path.Combine(temp.Path, "consumer");
        var consumerProject = Path.Combine(consumerDirectory, "Consumer.csproj");
        var packageCache = Path.Combine(temp.Path, "nuget-packages");
        var manifest = Path.Combine(consumerDirectory, "generated.contracts.json");
        var stateOutput = Path.Combine(consumerDirectory, "contracts-state.txt");
        var invocationLog = Path.Combine(consumerDirectory, "fake-tool.log");
        Directory.CreateDirectory(consumerDirectory);

        var (_, packageVersion) = await PackMainPackageAsync(repoRoot, packageOutput, packageCache);
        await WriteNuGetConfigAsync(consumerDirectory, packageOutput);
        var fakeTool = await CreateFakeContractsToolAsync(temp.Path);

        await File.WriteAllTextAsync(
            consumerProject,
            $$"""
              <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                      <TargetFramework>net10.0</TargetFramework>
                      <CaeriusContractsMode>Pull</CaeriusContractsMode>
                      <CaeriusContractsOutput>{{Escape(manifest)}}</CaeriusContractsOutput>
                      <CaeriusContractsToolCommand>dotnet &quot;{{Escape(fakeTool)}}&quot;</CaeriusContractsToolCommand>
                  </PropertyGroup>

                  <ItemGroup>
                      <PackageReference Include="CaeriusNet"
                                        Version="{{packageVersion}}"/>
                  </ItemGroup>

                  <Target Name="WriteContractsState" AfterTargets="CoreCompile">
                      <WriteLinesToFile File="{{Escape(stateOutput)}}"
                                        Lines="$(CaeriusContractsModeNormalized)"
                                        Overwrite="true"/>
                      <WriteLinesToFile File="{{Escape(stateOutput)}}"
                                        Lines="@(AdditionalFiles->'%(FullPath)|%(CaeriusContractManifest)')"
                                        Overwrite="false"/>
                  </Target>
              </Project>
              """);

        var buildResult = await RunDotnetAsync(
            consumerDirectory,
            new Dictionary<string, string>
            {
                ["CAERIUS_FAKE_TOOL_LOG"] = invocationLog,
                ["NUGET_PACKAGES"] = packageCache
            },
            "build",
            consumerProject,
            "--configuration",
            "Release");

        Assert.True(buildResult.ExitCode == 0, buildResult.ToString());
        Assert.True(File.Exists(manifest), $"Expected generated manifest at {manifest}.");

        var log = await File.ReadAllTextAsync(invocationLog);
        Assert.Contains("pull", log, StringComparison.Ordinal);
        Assert.Contains(manifest, log, StringComparison.Ordinal);

        var state = string.Join(Environment.NewLine, await File.ReadAllLinesAsync(stateOutput));
        Assert.Contains("PULL", state, StringComparison.Ordinal);
        Assert.Contains(Path.GetFullPath(manifest), state, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("|true", state, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TargetsAddManifestCreatedDuringMsbuildExecution()
    {
        var repoRoot = FindRepoRoot();
        var props = Path.Combine(repoRoot, "Src", "buildTransitive", "CaeriusNet.props");
        var targets = Path.Combine(repoRoot, "Src", "buildTransitive", "CaeriusNet.targets");

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
        var props = Path.Combine(repoRoot, "Src", "buildTransitive", "CaeriusNet.props");
        var targets = Path.Combine(repoRoot, "Src", "buildTransitive", "CaeriusNet.targets");

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

                  <Target Name="RunSmoke" DependsOnTargets="CaeriusPrepareContracts">
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
        var buildTransitive = Path.Combine(repoRoot, "Src", "buildTransitive");

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

    private static async Task<(string Package, string Version)> PackMainPackageAsync(
        string repoRoot,
        string packageOutput,
        string? packageCache = null)
    {
        Directory.CreateDirectory(packageOutput);

        var environment = packageCache is null
            ? null
            : new Dictionary<string, string>
            {
                ["NUGET_PACKAGES"] = packageCache
            };

        var toolBuild = await RunDotnetAsync(
            repoRoot,
            environment,
            "build",
            Path.Combine("Tools", "CaeriusNet.SqlServer.Contracts", "CaeriusNet.SqlServer.Contracts.csproj"),
            "--configuration",
            "Release",
            "--no-incremental");

        Assert.True(toolBuild.ExitCode == 0, toolBuild.ToString());

        var packageBuild = await RunDotnetAsync(
            repoRoot,
            environment,
            "build",
            Path.Combine("Src", "CaeriusNet.csproj"),
            "--configuration",
            "Release",
            "--no-incremental");

        Assert.True(packageBuild.ExitCode == 0, packageBuild.ToString());

        var packResult = await RunDotnetAsync(
            repoRoot,
            environment,
            "pack",
            Path.Combine("Src", "CaeriusNet.csproj"),
            "--configuration",
            "Release",
            "--no-build",
            "--output",
            packageOutput);

        Assert.True(packResult.ExitCode == 0, packResult.ToString());

        var package = Assert.Single(
            Directory.EnumerateFiles(packageOutput, "CaeriusNet.*.nupkg"),
            file => Path.GetFileName(file).StartsWith("CaeriusNet.", StringComparison.Ordinal) &&
                    !Path.GetFileName(file).StartsWith("CaeriusNet.SqlServer.Contracts.", StringComparison.Ordinal));

        return (package, GetMainPackageVersion(package));
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

    private static Task WriteNuGetConfigAsync(string directory, string packageOutput)
    {
        var localSource = Escape(Path.GetFullPath(packageOutput));

        return File.WriteAllTextAsync(
            Path.Combine(directory, "NuGet.config"),
            $$"""
              <?xml version="1.0" encoding="utf-8"?>
              <configuration>
                <packageSources>
                  <clear />
                  <add key="local-caeriusnet" value="{{localSource}}" />
                  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                </packageSources>
                <packageSourceMapping>
                  <packageSource key="local-caeriusnet">
                    <package pattern="CaeriusNet" />
                  </packageSource>
                  <packageSource key="nuget.org">
                    <package pattern="*" />
                  </packageSource>
                </packageSourceMapping>
              </configuration>
              """);
    }

    private static string GetMainPackageVersion(string package)
    {
        var fileName = Path.GetFileNameWithoutExtension(package);
        const string packagePrefix = "CaeriusNet.";
        Assert.True(fileName.StartsWith(packagePrefix, StringComparison.Ordinal), fileName);
        return fileName[packagePrefix.Length..];
    }

    private static async Task<string> CreateFakeContractsToolAsync(string root)
    {
        var projectDirectory = Path.Combine(root, "fake-tool");
        var project = Path.Combine(projectDirectory, "FakeContractsTool.csproj");
        Directory.CreateDirectory(projectDirectory);

        await File.WriteAllTextAsync(
            project,
            """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "Program.cs"),
            """
            string? output = null;
            for (var index = 0; index < args.Length - 1; index++)
                if (args[index] is "--output" or "--manifest")
                    output = args[index + 1];

            output ??= Path.Combine(Directory.GetCurrentDirectory(), "caerius.contracts.json");
            var directory = Path.GetDirectoryName(Path.GetFullPath(output));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(output, "{ \"version\": 1, \"namespace\": \"CaeriusNet.Generated\", \"database\": { \"name\": \"fake\", \"serverVersion\": \"fake\", \"compatibilityLevel\": 0 }, \"tableTypes\": [], \"procedures\": [] }");

            var log = Environment.GetEnvironmentVariable("CAERIUS_FAKE_TOOL_LOG");
            if (!string.IsNullOrWhiteSpace(log))
            {
                var logDirectory = Path.GetDirectoryName(Path.GetFullPath(log));
                if (!string.IsNullOrEmpty(logDirectory))
                    Directory.CreateDirectory(logDirectory);

                File.AppendAllText(log, string.Join(" ", args) + Environment.NewLine);
            }
            """);

        var result = await RunDotnetAsync(
            projectDirectory,
            "build",
            project,
            "--configuration",
            "Release");

        Assert.True(result.ExitCode == 0, result.ToString());
        return Path.Combine(projectDirectory, "bin", "Release", "net10.0", "FakeContractsTool.dll");
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
