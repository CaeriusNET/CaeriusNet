namespace CaeriusNet.Factories;

/// <summary>
///     Represents a factory for creating and managing database connections based on a provided connection string.
/// </summary>
internal sealed record CaeriusNetDbContext : ICaeriusNetDbContext
{

	/// <summary>
	///     Flag indicating whether logging is enabled.
	/// </summary>
	private readonly bool _isLoggingEnabled = LoggerProvider.GetLogger() != null;

	/// <summary>
	///     Logger instance for database operations.
	/// </summary>
	private readonly ILogger? _logger = LoggerProvider.GetLogger();

	private readonly Func<SqlConnection> _sqlConnectionFactory;

	public CaeriusNetDbContext(Func<SqlConnection> sqlConnectionFactory, IRedisCacheManager? redisCacheManager = null)
	{
		_sqlConnectionFactory = sqlConnectionFactory ?? throw new ArgumentNullException(nameof(sqlConnectionFactory));
		RedisCacheManager = redisCacheManager;
	}

	public IRedisCacheManager? RedisCacheManager { get; }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public SqlConnection DbConnection()
	{
		if (_isLoggingEnabled)
			_logger!.LogDatabaseConnecting();

		try{
			var connection = _sqlConnectionFactory();

			if (connection.State == ConnectionState.Closed)
				connection.Open();

			if (_isLoggingEnabled)
				_logger!.LogDatabaseConnected();

			return connection;
		}
		catch (SqlException ex){
			if (_isLoggingEnabled)
				_logger!.LogDatabaseConnectionFailed(ex);
			throw new CaeriusNetSqlException("Failed to open database connection", ex);
		}
	}
}