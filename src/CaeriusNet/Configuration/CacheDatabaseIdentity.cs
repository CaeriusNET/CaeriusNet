namespace CaeriusNet;

/// <summary>
///     Creates and validates the stable, non-secret identity which scopes shared cache keys to one SQL database.
/// </summary>
/// <remarks>
///     This is intentionally based only on <see cref="SqlConnectionStringBuilder.DataSource" /> and
///     <see cref="SqlConnectionStringBuilder.InitialCatalog" />. Credentials, tokens, application names and every
///     other connection-string setting are excluded. The length-delimited representation prevents ambiguities in
///     host/database pairs before the cache-key builder hashes it.
/// </remarks>
internal static class CaeriusCacheDatabaseIdentity
{
    internal static string? TryCreateFromConnectionString(string connectionString)
    {
        // This only parses the endpoint and catalog; it never opens a SQL connection or sends data on the wire.
        var builder = new SqlConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource.Trim();
        var initialCatalog = builder.InitialCatalog.Trim();
        if (dataSource.Length == 0 || initialCatalog.Length == 0)
            return null;

        return string.Concat(
            "sqlserver|",
            dataSource.Length.ToString(CultureInfo.InvariantCulture),
            ":",
            dataSource,
            "|",
            initialCatalog.Length.ToString(CultureInfo.InvariantCulture),
            ":",
            initialCatalog);
    }

    internal static void Validate(string identity, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity, parameterName);
        if (identity.Length > 512)
            throw new ArgumentOutOfRangeException(parameterName,
                "Cache database identity cannot exceed 512 characters.");

        foreach (var character in identity)
            if (char.IsControl(character) || character is ';' or '=')
                throw new ArgumentException(
                    "Cache database identity must be a stable, non-secret identifier, not a connection string.",
                    parameterName);
    }
}
