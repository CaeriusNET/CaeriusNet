using System.Text;
using Microsoft.Data.SqlClient;

namespace CaeriusNet.Exemples.Libs.Commons.Bootstrap;

/// <summary>
///     Idempotent SQL bootstrap that creates the schema, tables, TVP types, seed data and
///     stored procedures used by the CaeriusNet examples. The script lives at
///     <c>Sql/init.sql</c> and is embedded as a resource so callers don't need to ship it
///     separately.
/// </summary>
public static class ExampleDatabaseBootstrapper
{
    private const string ResourceName = "CaeriusNet.Exemples.Libs.Commons.Sql.init.sql";

    /// <summary>
    ///     Returns the embedded <c>init.sql</c> script as plain text. Useful for callers that
    ///     want to hand the script over to Aspire's <c>WithCreationScript</c>.
    /// </summary>
    public static string GetCreationScript()
    {
        var assembly = typeof(ExampleDatabaseBootstrapper).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"Embedded resource '{ResourceName}' not found. Available: " +
                               string.Join(", ", assembly.GetManifestResourceNames()));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    ///     Connects to <paramref name="connectionString" /> and applies the embedded
    ///     <c>init.sql</c> bootstrap script. Splits on top-level <c>GO</c> batch separators
    ///     because <see cref="SqlCommand" /> cannot execute them natively.
    /// </summary>
    public static async Task EnsureCreatedAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var script = GetCreationScript();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (var batch in SplitBatches(script))
        {
            if (string.IsNullOrWhiteSpace(batch))
                continue;

            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static IEnumerable<string> SplitBatches(string script)
    {
        // Match a line that contains only "GO" (case-insensitive), optionally surrounded by whitespace.
        var lines = script.Split('\n');
        var current = new StringBuilder(script.Length);

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (string.Equals(line.Trim(), "GO", StringComparison.OrdinalIgnoreCase))
            {
                yield return current.ToString();
                current.Clear();
                continue;
            }

            current.AppendLine(line);
        }

        if (current.Length > 0)
            yield return current.ToString();
    }
}
