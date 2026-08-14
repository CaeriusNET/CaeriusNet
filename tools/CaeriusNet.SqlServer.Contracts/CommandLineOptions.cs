using System.Globalization;

namespace CaeriusNet.SqlServer.Contracts;

internal sealed record CommandLineOptions(
    string? ConnectionEnv,
    string? ConnectionString,
    string? ConnectionName,
    string? ConfigurationBasePath,
    string? ConfigurationEnvironment,
    string? UserSecretsId,
    string? Output,
    string? Manifest,
    IReadOnlyList<string> Includes,
    IReadOnlyList<string> Excludes,
    int TimeoutSeconds)
{
    internal static CommandLineOptions Parse(string[] args)
    {
        string? connectionEnv = null;
        string? connectionString = null;
        string? connectionName = null;
        string? configurationBasePath = null;
        string? configurationEnvironment = null;
        string? userSecretsId = null;
        string? output = null;
        string? manifest = null;
        var includes = new List<string>();
        var excludes = new List<string>();
        var timeoutSeconds = 30;

        for (var i = 0; i < args.Length; i++)
        {
            var option = args[i];

            string Next()
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"Missing value for '{option}'.");

                return args[++i];
            }

            switch (option)
            {
                case "--connection-env":
                    connectionEnv = Next();
                    break;
                case "--connection-string":
                    connectionString = Next();
                    break;
                case "--connection-name":
                    connectionName = Next();
                    break;
                case "--configuration-base-path":
                    configurationBasePath = Next();
                    break;
                case "--configuration-environment":
                    configurationEnvironment = Next();
                    break;
                case "--user-secrets-id":
                    userSecretsId = Next();
                    break;
                case "--output":
                    output = Next();
                    break;
                case "--manifest":
                    manifest = Next();
                    break;
                case "--include":
                    includes.Add(Next());
                    break;
                case "--exclude":
                    excludes.Add(Next());
                    break;
                case "--timeout":
                    if (!int.TryParse(Next(), NumberStyles.None,
                            CultureInfo.InvariantCulture, out timeoutSeconds) || timeoutSeconds <= 0)
                        throw new ArgumentException("--timeout must be a positive number of seconds.");
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{option}'.");
            }
        }

        return new CommandLineOptions(
            connectionEnv,
            connectionString,
            connectionName,
            configurationBasePath,
            configurationEnvironment,
            userSecretsId,
            output,
            manifest,
            includes,
            excludes,
            timeoutSeconds);
    }
}
