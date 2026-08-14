namespace CaeriusNet;

public sealed class CaeriusNetOptions
{
    internal Func<IServiceProvider, CancellationToken, ValueTask<ICaeriusSqlConnectionLease>>? LeaseFactory
    {
        get;
        private set;
    }

    public string ApplicationNamespace { get; set; } = "application";

    /// <summary>
    ///     Stable, non-secret identity used to isolate cache keys for one SQL database.
    /// </summary>
    /// <remarks>
    ///     <see cref="UseSqlServer(string)" /> derives this value from the SQL Server data source and initial catalog.
    ///     Connection and lease factories must set it explicitly before using Redis, for example
    ///     <c>"orders-production-primary"</c>. Never assign a connection string, credential, token, cache key or
    ///     customer data.
    /// </remarks>
    public string? CacheDatabaseIdentity { get; set; }

    public bool CaptureParameterValues { get; set; }
    public ISet<string> ParameterCaptureAllowList { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public Func<string, object?, string>? ParameterRedactor { get; set; }

    public CaeriusNetOptions UseSqlServer(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        CacheDatabaseIdentity ??= CaeriusCacheDatabaseIdentity.TryCreateFromConnectionString(connectionString);
        var safeConnectionString = connectionString;
        LeaseFactory = (_, _) => ValueTask.FromResult<ICaeriusSqlConnectionLease>(
            new OwnedSqlConnectionLease(new SqlConnection(safeConnectionString)));
        return this;
    }

    public CaeriusNetOptions UseSqlServer(Func<IServiceProvider, SqlConnection> connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        LeaseFactory = (services, _) => ValueTask.FromResult<ICaeriusSqlConnectionLease>(
            new OwnedSqlConnectionLease(
                connectionFactory(services) ??
                throw new InvalidOperationException("The SQL connection factory returned null.")));
        return this;
    }

    public CaeriusNetOptions UseSqlServerLease(
        Func<IServiceProvider, CancellationToken, ValueTask<ICaeriusSqlConnectionLease>> leaseFactory)
    {
        ArgumentNullException.ThrowIfNull(leaseFactory);
        LeaseFactory = leaseFactory;
        return this;
    }

    internal void Validate(string optionsName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ApplicationNamespace);
        if (CacheDatabaseIdentity is { } cacheDatabaseIdentity)
            CaeriusCacheDatabaseIdentity.Validate(cacheDatabaseIdentity, nameof(CacheDatabaseIdentity));
        if (LeaseFactory is null)
            throw new InvalidOperationException(
                $"CaeriusNet database '{optionsName}' has no SQL Server connection. Call UseSqlServer(...).");
        if (CaptureParameterValues && (ParameterCaptureAllowList.Count == 0 || ParameterRedactor is null))
            throw new InvalidOperationException(
                "Parameter capture requires both a non-empty allowlist and an explicit redaction delegate.");
    }
}

public sealed class CaeriusMemoryCacheOptions
{
    public long SizeLimit { get; set; } = 1024;
}

internal sealed class OwnedSqlConnectionLease(SqlConnection connection) : ICaeriusSqlConnectionLease
{
    public SqlConnection Connection { get; } = connection;

    public ValueTask DisposeAsync()
    {
        return Connection.DisposeAsync();
    }
}
