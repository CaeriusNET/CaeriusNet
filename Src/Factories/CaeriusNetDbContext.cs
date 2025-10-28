namespace CaeriusNet.Factories;

/// <summary>
///     Represents a factory for creating and managing database connections based on a provided connection string.
/// </summary>
internal sealed record CaeriusNetDbContext : ICaeriusNetDbContext
{
	/// <summary>
	///     The connection string builder used to build SQL Server connection strings.
	/// </summary>
	/// <remarks>
	///     Provides a way to construct a valid Microsoft SQL Server connection string by adding or modifying individual
	///     connection properties.
	///     The builder maintains a collection of properties that can be modified and generates the resulting connection
	///     string.
	///     This class is thread-safe for multiple readers and a single writer.
	/// </remarks>
	/// <seealso cref="System.Data.SqlClient.SqlConnection" />
	/// <seealso cref="System.Data.Common.DbConnectionStringBuilder" />
	private readonly SqlConnectionStringBuilder _connectionStringBuilder;

	/// <summary>
	///     Flag indicating whether logging is enabled.
	/// </summary>
	private readonly bool _isLoggingEnabled = LoggerProvider.GetLogger() != null;

	/// <summary>
	///     Logger instance for database operations.
	/// </summary>
	private readonly ILogger? _logger = LoggerProvider.GetLogger();

	public CaeriusNetDbContext(string connectionString)
	{
		_connectionStringBuilder = new SqlConnectionStringBuilder(connectionString)
		{
			Pooling = true,
			MinPoolSize = 0,
			MaxPoolSize = 100,
			ConnectRetryCount = 3,
			ConnectRetryInterval = 2,
			ConnectTimeout = 15,
			ApplicationIntent = ApplicationIntent.ReadWrite,
			MultiSubnetFailover = false,
			MultipleActiveResultSets = false,
			Enlist = false
		};
	}

	/// <summary>
	///     Creates and opens a database connection.
	/// </summary>
	/// <returns>An open <see cref="IDbConnection" />.</returns>
	/// <exception cref="CaeriusNetSqlException">Thrown when the connection fails to open.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public IDbConnection DbConnection()
	{
		if (_isLoggingEnabled)
			_logger!.LogDatabaseConnecting();

		try{
			var connection = new SqlConnection(_connectionStringBuilder.ConnectionString);
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