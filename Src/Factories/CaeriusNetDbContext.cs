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

        SqlConnection? connection = null;
        try
        {
            connection = _sqlConnectionFactory()
                         ?? throw new InvalidOperationException("SQL connection factory returned null.");

            if (connection.State == ConnectionState.Closed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (connection.State != ConnectionState.Open)
                throw new InvalidOperationException("SQL connection factory returned a non-open connection.");

            if (_isLoggingEnabled)
                _logger!.LogDatabaseConnected();

            var openedConnection = connection;
            connection = null;
            return openedConnection;
        }
        catch (SqlException ex)
        {
            await DisposeFailedConnectionAsync(connection).ConfigureAwait(false);
            if (_isLoggingEnabled)
                _logger!.LogDatabaseConnectionFailed(ex);
            throw new CaeriusNetSqlException("Failed to open database connection", ex);
        }
        catch
        {
            await DisposeFailedConnectionAsync(connection).ConfigureAwait(false);
            throw;
        }
    }

    private static async ValueTask DisposeFailedConnectionAsync(SqlConnection? connection)
    {
        if (connection is null)
            return;

        try
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Preserve the original open/factory exception; cleanup is best-effort here.
        }
    }
}
