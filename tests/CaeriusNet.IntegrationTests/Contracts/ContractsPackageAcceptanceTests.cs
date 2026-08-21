using System.Diagnostics;
using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using CaeriusNet.IntegrationTests.Infrastructure;
using DotNet.Testcontainers.Builders;
using Xunit.Sdk;

namespace CaeriusNet.IntegrationTests.Contracts;

/// <summary>
///     End-to-end acceptance for the package-owned AutoContracts MSBuild integration.  The temporary
///     consumer uses only a PackageReference to the release nupkg; it never references this repository.
/// </summary>
public sealed class ContractsPackageAcceptanceTests
{
    [Theory]
    [MemberData(nameof(ContainerTestGate.SqlServerImages), MemberType = typeof(ContainerTestGate))]
    public async Task PackedToolPullsV3ManifestAndVerifyReportsDriftWithoutLeakingConnectionSecrets(string image)
    {
        ContainerTestGate.RequireEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var container = new MsSqlBuilder(image)
            .WithPassword("Caerius!Tests_2026")
            .Build();
        try
        {
            await container.StartAsync(cancellationToken);
        }
        catch (DockerUnavailableException exception)
        {
            throw SkipException.ForSkip($"Docker is unavailable for Contracts package acceptance: {exception.Message}");
        }
        catch (DockerConfigurationException exception)
        {
            throw SkipException.ForSkip(
                $"Docker is not configured for Contracts package acceptance: {exception.Message}");
        }
        catch (TimeoutException exception)
        {
            throw SkipException.ForSkip(
                $"SQL Server container timed out for Contracts package acceptance: {exception.Message}");
        }

        var connectionString = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            TrustServerCertificate = true
        }.ConnectionString;
        await ProvisionAsync(connectionString, cancellationToken);

        var package = RequireContractsPackage();
        var packageVersion = PackageVersion.FromPackagePath(package);
        using var temporaryDirectory = new TemporaryDirectory();
        var consumerDirectory = Path.Combine(temporaryDirectory.Path, "consumer");
        Directory.CreateDirectory(consumerDirectory);
        var manifest = Path.Combine(consumerDirectory, "caerius.contracts.json");
        var consumerProject = Path.Combine(consumerDirectory, "ContractsConsumer.csproj");
        await WriteNuGetConfigAsync(consumerDirectory, Path.GetDirectoryName(package)!, cancellationToken);
        await WriteConsumerProjectAsync(consumerProject, packageVersion, manifest, cancellationToken);

        const string connectionEnvironmentVariable = "CAERIUS_CONTRACTS_PACKAGE_CONNECTION";
        var environment = new Dictionary<string, string>
        {
            ["NUGET_PACKAGES"] = Path.Combine(temporaryDirectory.Path, "nuget-cache"),
            [connectionEnvironmentVariable] = connectionString
        };

        var pull = await RunDotnetAsync(
            consumerDirectory,
            environment,
            cancellationToken,
            "build",
            consumerProject,
            "--configuration",
            "Release",
            "--nologo");

        Assert.True(pull.ExitCode == 0, pull.ToString());
        Assert.False(ContainsSecret(pull, connectionString),
            "Pull diagnostics must never contain the connection string.");
        Assert.True(File.Exists(manifest), "The packaged Pull target must create the v3 manifest.");
        var originalManifest = await File.ReadAllTextAsync(manifest, cancellationToken);
        Assert.DoesNotContain(connectionString, originalManifest,
            StringComparison.Ordinal);
        await AssertV3ManifestAsync(manifest, cancellationToken);

        var tamperedManifest = JsonNode.Parse(originalManifest)?.AsObject()
                               ?? throw new InvalidOperationException("The pulled manifest is not a JSON object.");
        var firstProcedure = tamperedManifest["procedures"]?.AsArray().FirstOrDefault()?.AsObject()
                             ?? throw new InvalidOperationException("The pulled manifest has no procedure.");
        var firstColumn = firstProcedure["resultSets"]?.AsArray().FirstOrDefault()?["columns"]?.AsArray()
                              .FirstOrDefault()?.AsObject()
                          ?? throw new InvalidOperationException("The pulled manifest has no result column.");
        firstColumn["clrType"] = "long";
        await File.WriteAllTextAsync(manifest, tamperedManifest.ToJsonString(), cancellationToken);

        var staleHashVerify = await RunDotnetAsync(
            consumerDirectory,
            environment,
            cancellationToken,
            "build",
            consumerProject,
            "--configuration",
            "Release",
            "--no-restore",
            "--nologo",
            "-p:CaeriusContractsMode=Verify");

        Assert.NotEqual(0, staleHashVerify.ExitCode);
        Assert.Contains("Stored hash does not match", staleHashVerify.AllOutput, StringComparison.Ordinal);
        Assert.False(ContainsSecret(staleHashVerify, connectionString),
            "Stored-hash diagnostics must never contain the connection string.");
        await File.WriteAllTextAsync(manifest, originalManifest, cancellationToken);

        await AlterContractAsync(connectionString, cancellationToken);
        var verify = await RunDotnetAsync(
            consumerDirectory,
            environment,
            cancellationToken,
            "build",
            consumerProject,
            "--configuration",
            "Release",
            "--no-restore",
            "--nologo",
            "-p:CaeriusContractsMode=Verify");

        Assert.NotEqual(0, verify.ExitCode);
        Assert.Contains("CAERIUS209 error", verify.AllOutput, StringComparison.Ordinal);
        Assert.False(ContainsSecret(verify, connectionString),
            "Verify diagnostics must never contain the connection string.");
    }

    private static async Task ProvisionAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_contracts_package_probe
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           SELECT CAST(12 AS int) AS Version;
                                       END
                                       """, cancellationToken);
    }

    private static async Task AlterContractAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_contracts_package_probe
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           SELECT CAST(12 AS int) AS Version,
                                                  CAST(N'drift' AS nvarchar(20)) AS Drift;
                                       END
                                       """, cancellationToken);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AssertV3ManifestAsync(string manifest, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(manifest);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        Assert.Equal(3, root.GetProperty("version").GetInt32());
        Assert.Equal("CaeriusNet.Generated", root.GetProperty("namespace").GetString());
        Assert.True(root.GetProperty("serverMajor").GetInt32() is 16 or 17);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("features").ValueKind);
        Assert.Contains(
            root.GetProperty("procedures").EnumerateArray(),
            static procedure => procedure.GetProperty("schema").GetString() == "dbo" &&
                                procedure.GetProperty("name").GetString() == "caerius_test_contracts_package_probe");
    }

    private static bool ContainsSecret(CommandResult result, string secret)
    {
        return result.AllOutput.Contains(secret, StringComparison.Ordinal);
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
                  <packageSource key="caerius-artifacts"><package pattern="CaeriusNet*" /></packageSource>
                  <packageSource key="nuget.org"><package pattern="*" /></packageSource>
                </packageSourceMapping>
              </configuration>
              """,
            cancellationToken);
    }

    private static Task WriteConsumerProjectAsync(
        string consumerProject,
        string packageVersion,
        string manifest,
        CancellationToken cancellationToken)
    {
        return File.WriteAllTextAsync(
            consumerProject,
            $$"""
              <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                  <TargetFramework>net10.0</TargetFramework>
                  <CaeriusContractsMode>Pull</CaeriusContractsMode>
                  <CaeriusContractsOutput>{{Escape(manifest)}}</CaeriusContractsOutput>
                  <CaeriusContractsConnectionStringEnv>CAERIUS_CONTRACTS_PACKAGE_CONNECTION</CaeriusContractsConnectionStringEnv>
                  <CaeriusContractsTimeout>30</CaeriusContractsTimeout>
                </PropertyGroup>
                <ItemGroup>
                  <PackageReference Include="CaeriusNet.Contracts" Version="{{packageVersion}}" />
                </ItemGroup>
              </Project>
              """,
            cancellationToken);
    }

    private static async Task<CommandResult> RunDotnetAsync(
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
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
        foreach (var (name, value) in environment)
            startInfo.Environment[name] = value;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
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
        internal string AllOutput => StandardOutput + Environment.NewLine + StandardError;

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
