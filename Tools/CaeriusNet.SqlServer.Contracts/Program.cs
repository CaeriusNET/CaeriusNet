using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaeriusNet.SqlServer.Contracts;

internal static class Program
{
    internal const string DefaultManifestPath = "caerius.contracts.json";

    internal static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            WriteUsage();
            return 0;
        }

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            var command = args[0];
            var options = CommandLineOptions.Parse(args.Skip(1).ToArray());

            return command switch
            {
                "pull" => await PullAsync(options, cancellation.Token).ConfigureAwait(false),
                "verify" => await VerifyAsync(options, cancellation.Token).ConfigureAwait(false),
                _ => UnknownCommand(command)
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation canceled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static async Task<int> PullAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        var output = options.Output ?? options.Manifest ?? DefaultManifestPath;
        var diagnostics = new ContractDiagnosticSink();
        var manifest = await SqlServerContractDiscoverer.DiscoverAsync(options, diagnostics, cancellationToken)
            .ConfigureAwait(false);
        ContractDiagnosticWriter.Write(diagnostics);

        await WriteManifestAsync(output, manifest, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Wrote {output}");

        return diagnostics.HasErrors ? 1 : 0;
    }

    private static async Task<int> VerifyAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        var manifestPath = options.Manifest ?? options.Output ?? DefaultManifestPath;
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"Manifest not found: {manifestPath}");
            return 1;
        }

        var existingJson = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        var existingManifest = JsonSerializer.Deserialize<ContractManifest>(existingJson, ManifestJsonOptions)
                               ?? throw new InvalidOperationException($"Manifest could not be parsed: {manifestPath}");

        var diagnostics = new ContractDiagnosticSink();
        var discovered = await SqlServerContractDiscoverer.DiscoverAsync(options, diagnostics, cancellationToken)
            .ConfigureAwait(false);
        ContractDiagnosticWriter.Write(diagnostics);

        if (!ReportVerifyDiagnostics(existingManifest, discovered))
        {
            Console.WriteLine("Contract manifest is up to date.");
            return diagnostics.HasErrors ? 1 : 0;
        }

        return diagnostics.HasErrors ? 1 : 2;
    }

    private static async Task WriteManifestAsync(
        string path,
        ContractManifest manifest,
        CancellationToken cancellationToken)
    {
        var absolutePath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(
            directory ?? Directory.GetCurrentDirectory(),
            "." + Path.GetFileName(absolutePath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            var json = JsonSerializer.Serialize(manifest, ManifestJsonOptions);
            await File.WriteAllTextAsync(
                    tempPath,
                    json + Environment.NewLine,
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);

            File.Move(tempPath, absolutePath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static bool ReportVerifyDiagnostics(ContractManifest existing, ContractManifest discovered)
    {
        var hasDifferences = false;
        if (existing.Version != discovered.Version)
        {
            hasDifferences = true;
            Console.Error.WriteLine(
                $"CAERIUS209 error: Contract manifest version mismatch. Expected '{discovered.Version}', found '{existing.Version}'.");
        }

        if (!string.Equals(existing.Namespace, discovered.Namespace, StringComparison.Ordinal))
        {
            hasDifferences = true;
            Console.Error.WriteLine(
                $"CAERIUS209 error: Contract manifest namespace mismatch. Expected '{discovered.Namespace}', found '{existing.Namespace}'.");
        }

        hasDifferences |= ReportTableTypeDiagnostics(existing, discovered);
        hasDifferences |= ReportProcedureDiagnostics(existing, discovered);

        return hasDifferences;
    }

    private static bool ReportTableTypeDiagnostics(ContractManifest existing, ContractManifest discovered)
    {
        var hasDifferences = false;
        var existingTableTypes = existing.TableTypes.ToDictionary(
            tableType => BuildFullName(tableType.Schema, tableType.Name),
            StringComparer.OrdinalIgnoreCase);
        var discoveredTableTypes = discovered.TableTypes.ToDictionary(
            tableType => BuildFullName(tableType.Schema, tableType.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (tableTypeName, discoveredTableType) in discoveredTableTypes)
        {
            if (!existingTableTypes.TryGetValue(tableTypeName, out var existingTableType))
            {
                hasDifferences = true;
                Console.Error.WriteLine(
                    $"CAERIUS209 error: Table type '{tableTypeName}' is missing from the CaeriusNet contract manifest.");
                continue;
            }

            if (!string.Equals(
                    existingTableType.ContractHash,
                    discoveredTableType.ContractHash,
                    StringComparison.Ordinal))
            {
                hasDifferences = true;
                Console.Error.WriteLine($"CAERIUS209 error: Contract hash mismatch for table type '{tableTypeName}'.");
            }
        }

        foreach (var tableTypeName in existingTableTypes.Keys)
            if (!discoveredTableTypes.ContainsKey(tableTypeName))
            {
                hasDifferences = true;
                Console.Error.WriteLine($"CAERIUS209 error: Table type '{tableTypeName}' is no longer discovered.");
            }

        return hasDifferences;
    }

    private static bool ReportProcedureDiagnostics(ContractManifest existing, ContractManifest discovered)
    {
        var hasDifferences = false;
        var existingProcedures = existing.Procedures.ToDictionary(
            procedure => BuildFullName(procedure.Schema, procedure.Name),
            StringComparer.OrdinalIgnoreCase);
        var discoveredProcedures = discovered.Procedures.ToDictionary(
            procedure => BuildFullName(procedure.Schema, procedure.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (procedureName, discoveredProcedure) in discoveredProcedures)
        {
            if (!existingProcedures.TryGetValue(procedureName, out var existingProcedure))
            {
                hasDifferences = true;
                Console.Error.WriteLine(
                    $"CAERIUS201 error: Procedure '{procedureName}' is missing from the CaeriusNet contract manifest.");
                continue;
            }

            if (!string.Equals(
                    existingProcedure.ContractHash,
                    discoveredProcedure.ContractHash,
                    StringComparison.Ordinal))
            {
                hasDifferences = true;
                Console.Error.WriteLine($"CAERIUS209 error: Contract hash mismatch for '{procedureName}'.");
            }
        }

        foreach (var procedureName in existingProcedures.Keys)
            if (!discoveredProcedures.ContainsKey(procedureName))
            {
                hasDifferences = true;
                Console.Error.WriteLine($"CAERIUS209 error: Procedure '{procedureName}' is no longer discovered.");
            }

        return hasDifferences;
    }

    private static string BuildFullName(string schema, string name)
    {
        return schema + "." + name;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        WriteUsage();
        return 1;
    }

    private static void WriteUsage()
    {
        Console.WriteLine($"""
                           CaeriusNet SQL Server contracts

                           Commands:
                             pull   --connection-name DefaultConnection --output {DefaultManifestPath}
                             verify --connection-name DefaultConnection --manifest {DefaultManifestPath}

                           Options:
                             --connection-env <name>      Environment variable containing the SQL Server connection string.
                             --connection-string <value>  SQL Server connection string. Overrides --connection-env.
                             --connection-name <name>     Reads ConnectionStrings:<name> from .NET configuration.
                                                          Defaults to DefaultConnection.
                             --configuration-base-path <path>
                                                          Directory containing appsettings.json. Defaults to the current directory.
                             --configuration-environment <name>
                                                          Loads appsettings.<name>.json. Defaults to DOTNET_ENVIRONMENT or ASPNETCORE_ENVIRONMENT.
                             --user-secrets-id <id>       Optional user secrets id used with --connection-name.
                             --output <path>              Output manifest path for pull. Defaults to {DefaultManifestPath}.
                             --manifest <path>            Manifest path for verify.
                           """);
    }
}
