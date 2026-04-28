namespace CaeriusNet.Factories;

/// <summary>
///     Open SQL Server connections for CaeriusNet commands.
/// </summary>
internal sealed record CaeriusNetDbContext : ICaeriusNetDbContext
{
    private readonly bool _isLoggingEnabled;
    private readonly ILogger? _logger;
    private readonly Func<SqlConnection> _sqlConnectionFactory;

    public CaeriusNetDbContext(Func<SqlConnection> sqlConnectionFactory, IRedisCacheManager? redisCacheManager = null)
    {
        _sqlConnectionFactory = sqlConnectionFactory ?? throw new ArgumentNullException(nameof(sqlConnectionFactory));
        RedisCacheManager = redisCacheManager;
        _logger = LoggerProvider.GetLogger();
        _isLoggingEnabled = _logger != null;
    }

    public IRedisCacheManager? RedisCacheManager { get; }

    /// <summary>
    ///     Create and open a SQL connection asynchronously.
    /// </summary>
    public async ValueTask<SqlConnection> DbConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoggingEnabled)
            _logger!.LogDatabaseConnecting();

        try
        {
            var connection = _sqlConnectionFactory();

            if (connection.State == ConnectionState.Closed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (_isLoggingEnabled)
                _logger!.LogDatabaseConnected();

            return connection;
        }
        catch (SqlException ex)
        {
            if (_isLoggingEnabled)
                _logger!.LogDatabaseConnectionFailed(ex);
            throw new CaeriusNetSqlException("Failed to open database connection", ex);
        }
    }
}
