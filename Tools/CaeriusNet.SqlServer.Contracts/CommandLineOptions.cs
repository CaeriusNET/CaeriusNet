namespace CaeriusNet.SqlServer.Contracts;

internal sealed record CommandLineOptions(
    string? ConnectionEnv,
    string? ConnectionString,
    string[] Schemas,
    string? Output,
    string? Manifest)
{
    internal static CommandLineOptions Parse(string[] args)
    {
        string? connectionEnv = null;
        string? connectionString = null;
        string[] schemas = [];
        string? output = null;
        string? manifest = null;

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
                case "--schemas":
                    schemas = Next()
                        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                case "--output":
                    output = Next();
                    break;
                case "--manifest":
                    manifest = Next();
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{option}'.");
            }
        }

        return new CommandLineOptions(connectionEnv, connectionString, schemas, output, manifest);
    }
}