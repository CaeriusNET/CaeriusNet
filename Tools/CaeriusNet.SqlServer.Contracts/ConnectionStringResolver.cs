using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CaeriusNet.SqlServer.Contracts;

internal static class ConnectionStringResolver
{
    private const string DefaultConnectionName = "DefaultConnection";

    internal static string Resolve(CommandLineOptions options)
    {
        var connectionString = ResolveRawConnectionString(options);
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationIntent = ApplicationIntent.ReadOnly
        };

        return builder.ConnectionString;
    }

    private static string ResolveRawConnectionString(CommandLineOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            return options.ConnectionString;

        if (!string.IsNullOrWhiteSpace(options.ConnectionEnv))
            return ResolveFromEnvironment(options.ConnectionEnv);

        if (!string.IsNullOrWhiteSpace(options.ConnectionName))
            return ResolveFromConfiguration(options);

        return ResolveFromConfiguration(options with { ConnectionName = DefaultConnectionName });
    }

    private static string ResolveFromEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Environment variable '{name}' does not contain a SQL Server connection string.");

        return value;
    }

    private static string ResolveFromConfiguration(CommandLineOptions options)
    {
        var connectionName = options.ConnectionName!;
        var configuration = BuildConfiguration(options);
        var value = configuration.GetConnectionString(connectionName);

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Connection string 'ConnectionStrings:{connectionName}' was not found in .NET configuration.");

        return value;
    }

    private static IConfigurationRoot BuildConfiguration(CommandLineOptions options)
    {
        var basePath = string.IsNullOrWhiteSpace(options.ConfigurationBasePath)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(options.ConfigurationBasePath);
        var environment = ResolveEnvironment(options.ConfigurationEnvironment);
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        if (!string.IsNullOrWhiteSpace(environment))
            builder.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);

        if (!string.IsNullOrWhiteSpace(options.UserSecretsId))
            builder.AddJsonFile(GetUserSecretsPath(options.UserSecretsId), optional: true, reloadOnChange: false);

        builder.AddEnvironmentVariables();

        return builder.Build();
    }

    private static string GetUserSecretsPath(string userSecretsId)
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
                return Path.Combine(appData, "Microsoft", "UserSecrets", userSecretsId, "secrets.json");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".microsoft", "usersecrets", userSecretsId, "secrets.json");
    }

    private static string? ResolveEnvironment(string? explicitEnvironment)
    {
        if (!string.IsNullOrWhiteSpace(explicitEnvironment))
            return explicitEnvironment;

        var dotnetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(dotnetEnvironment))
            return dotnetEnvironment;

        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    }
}
