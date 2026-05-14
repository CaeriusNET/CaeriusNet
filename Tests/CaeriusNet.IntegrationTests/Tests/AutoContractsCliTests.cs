using System.Diagnostics;
using System.Text.Json;

namespace CaeriusNet.IntegrationTests.Tests;

/// <summary>
///     Exercises the SQL Server metadata CLI against the Docker-backed integration database.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class AutoContractsCliTests(SqlServerFixture fixture)
{
    [SqlServerAvailableFact]
    public async Task Cli_Pull_And_Verify_Generates_Manifest_From_ReadOnly_Metadata()
    {
        await SetExecutionProbeAsync(0);

        var repoRoot = FindRepoRoot();
        var toolProject = Path.Combine(repoRoot, "Tools", "CaeriusNet.SqlServer.Contracts",
            "CaeriusNet.SqlServer.Contracts.csproj");
        var manifestPath = Path.Combine(Path.GetTempPath(), "caerius-contracts-" + Guid.NewGuid().ToString("N"),
            "caerius.contracts.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);

        try
        {
            var pull = await RunDotnetAsync(
                repoRoot,
                "run",
                "--project",
                toolProject,
                "--",
                "pull",
                "--connection-string",
                fixture.ConnectionString,
                "--schemas",
                "contracts",
                "--output",
                manifestPath);

            Assert.True(pull.ExitCode == 0, pull.ToString());
            Assert.True(File.Exists(manifestPath), $"Expected manifest to be written at {manifestPath}.");

            await AssertManifestShapeAsync(manifestPath);

            var verify = await RunDotnetAsync(
                repoRoot,
                "run",
                "--project",
                toolProject,
                "--",
                "verify",
                "--connection-string",
                fixture.ConnectionString,
                "--schemas",
                "contracts",
                "--manifest",
                manifestPath);

            Assert.True(verify.ExitCode == 0, verify.ToString());
            Assert.Equal(0, await ReadExecutionProbeAsync());
        }
        finally
        {
            var directory = Path.GetDirectoryName(manifestPath);
            if (directory is not null && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [SqlServerAvailableFact]
    public async Task Cli_Verify_Reports_Missing_Procedure_And_Drift()
    {
        var repoRoot = FindRepoRoot();
        var toolProject = Path.Combine(repoRoot, "Tools", "CaeriusNet.SqlServer.Contracts",
            "CaeriusNet.SqlServer.Contracts.csproj");
        var manifestPath = Path.Combine(Path.GetTempPath(), "caerius-contracts-" + Guid.NewGuid().ToString("N"),
            "caerius.contracts.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(manifestPath, """
                                                   {
                                                     "version": 1,
                                                     "namespace": "CaeriusNet.Generated",
                                                     "database": {
                                                       "name": "missing",
                                                       "serverVersion": "missing",
                                                       "compatibilityLevel": 0
                                                     },
                                                     "tableTypes": [],
                                                     "procedures": []
                                                   }
                                                   """);

        try
        {
            var verify = await RunDotnetAsync(
                repoRoot,
                "run",
                "--project",
                toolProject,
                "--",
                "verify",
                "--connection-string",
                fixture.ConnectionString,
                "--schemas",
                "contracts",
                "--manifest",
                manifestPath);

            Assert.Equal(2, verify.ExitCode);
            Assert.Contains("CAERIUS201 error", verify.StandardError, StringComparison.Ordinal);
            Assert.Contains("CAERIUS209 error", verify.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            var directory = Path.GetDirectoryName(manifestPath);
            if (directory is not null && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static async Task AssertManifestShapeAsync(string manifestPath)
    {
        await using var stream = File.OpenRead(manifestPath);
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("version").GetInt32());

        var tableType = Assert.Single(root.GetProperty("tableTypes").EnumerateArray());
        Assert.Equal("contracts", tableType.GetProperty("schema").GetString());
        Assert.Equal("WidgetTvp", tableType.GetProperty("name").GetString());
        Assert.Equal("decimal(18,4)", tableType.GetProperty("columns")[3].GetProperty("sqlType").GetString());
        Assert.Equal(18, tableType.GetProperty("columns")[3].GetProperty("precision").GetInt32());
        Assert.Equal(4, tableType.GetProperty("columns")[3].GetProperty("scale").GetInt32());

        var procedures = root.GetProperty("procedures").EnumerateArray().ToArray();
        Assert.Contains(procedures, procedure =>
            procedure.GetProperty("schema").GetString() == "contracts" &&
            procedure.GetProperty("name").GetString() == "usp_SearchWidgets");
        Assert.Contains(procedures, procedure =>
            procedure.GetProperty("schema").GetString() == "contracts" &&
            procedure.GetProperty("name").GetString() == "usp_PreviewWidgetBatch");
        Assert.Contains(procedures, procedure =>
            procedure.GetProperty("schema").GetString() == "contracts" &&
            procedure.GetProperty("name").GetString() == "usp_QuoteWidget");
    }

    private async Task SetExecutionProbeAsync(int value)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE dbo.AutoContractsExecutionProbe SET ExecutionCount = @Value;";
        command.Parameters.Add(new SqlParameter("@Value", SqlDbType.Int) { Value = value });
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> ReadExecutionProbeAsync()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ExecutionCount FROM dbo.AutoContractsExecutionProbe;";
        var value = await command.ExecuteScalarAsync();
        return Assert.IsType<int>(value);
    }

    private static async Task<CommandResult> RunDotnetAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

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
}
