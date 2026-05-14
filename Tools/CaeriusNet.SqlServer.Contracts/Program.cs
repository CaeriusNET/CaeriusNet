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

        try
        {
            var command = args[0];
            var options = CommandLineOptions.Parse(args.Skip(1).ToArray());

            return command switch
            {
                "pull" => await PullAsync(options).ConfigureAwait(false),
                "verify" => await VerifyAsync(options).ConfigureAwait(false),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task<int> PullAsync(CommandLineOptions options)
    {
        var output = options.Output ?? options.Manifest ?? DefaultManifestPath;
        var diagnostics = new ContractDiagnosticSink();
        var manifest = await SqlServerContractDiscoverer.DiscoverAsync(options, diagnostics).ConfigureAwait(false);
        ContractDiagnosticWriter.Write(diagnostics);

        var json = JsonSerializer.Serialize(manifest, ManifestJsonOptions);
        var directory = Path.GetDirectoryName(Path.GetFullPath(output));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(output, json + Environment.NewLine, Encoding.UTF8).ConfigureAwait(false);
        Console.WriteLine($"Wrote {output}");

        return diagnostics.HasErrors ? 1 : 0;
    }

    private static async Task<int> VerifyAsync(CommandLineOptions options)
    {
        var manifestPath = options.Manifest ?? options.Output ?? DefaultManifestPath;
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"Manifest not found: {manifestPath}");
            return 1;
        }

        var diagnostics = new ContractDiagnosticSink();
        var discovered = await SqlServerContractDiscoverer.DiscoverAsync(options, diagnostics).ConfigureAwait(false);
        ContractDiagnosticWriter.Write(diagnostics);

        var discoveredJson = JsonSerializer.Serialize(discovered, ManifestJsonOptions);
        var existingJson = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
        var existingManifest = JsonSerializer.Deserialize<ContractManifest>(existingJson, ManifestJsonOptions)
                               ?? throw new InvalidOperationException($"Manifest could not be parsed: {manifestPath}");
        var normalizedExistingJson = JsonSerializer.Serialize(existingManifest, ManifestJsonOptions);

        if (string.Equals(discoveredJson, normalizedExistingJson, StringComparison.Ordinal))
        {
            Console.WriteLine("Contract manifest is up to date.");
            return diagnostics.HasErrors ? 1 : 0;
        }

        ReportVerifyDiagnostics(existingManifest, discovered);
        return diagnostics.HasErrors ? 1 : 2;
    }

    private static void ReportVerifyDiagnostics(ContractManifest existing, ContractManifest discovered)
    {
        var existingProcedures = existing.Procedures.ToDictionary(
            procedure => BuildFullName(procedure.Schema, procedure.Name),
            StringComparer.OrdinalIgnoreCase);
        var discoveredProcedures = discovered.Procedures.ToDictionary(
            procedure => BuildFullName(procedure.Schema, procedure.Name),
            StringComparer.OrdinalIgnoreCase);

        var emittedHashMismatch = false;
        foreach (var (procedureName, discoveredProcedure) in discoveredProcedures)
        {
            if (!existingProcedures.TryGetValue(procedureName, out var existingProcedure))
            {
                Console.Error.WriteLine(
                    $"CAERIUS201 error: Procedure '{procedureName}' is missing from the CaeriusNet contract manifest.");
                continue;
            }

            if (!string.Equals(
                    existingProcedure.ContractHash,
                    discoveredProcedure.ContractHash,
                    StringComparison.Ordinal))
            {
                emittedHashMismatch = true;
                Console.Error.WriteLine($"CAERIUS209 error: Contract hash mismatch for '{procedureName}'.");
            }
        }

        foreach (var procedureName in existingProcedures.Keys)
        {
            if (!discoveredProcedures.ContainsKey(procedureName))
            {
                emittedHashMismatch = true;
                Console.Error.WriteLine($"CAERIUS209 error: Contract hash mismatch for '{procedureName}'.");
            }
        }

        if (!emittedHashMismatch)
            Console.Error.WriteLine("CAERIUS209 error: Contract hash mismatch for 'caerius.contracts.json'.");
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
