namespace CaeriusNet.Factories;

/// <summary>
///     Represents a factory for creating and managing database connections based on a provided connection string.
/// </summary>
internal sealed record CaeriusNetDbContext(string ConnectionString) : ICaeriusNetDbContext
{
	private readonly ICaeriusNetLogger? _logger = LoggerProvider.GetLogger();

	/// <summary>
	///     Creates and opens a database connection.
	/// </summary>
	/// <returns>An open <see cref="IDbConnection" />.</returns>
	/// <exception cref="CaeriusNetSqlException">Thrown when the connection fails to open.</exception>
	public IDbConnection DbConnection()
	{
		_logger?.LogDebug(LogCategory.Database, "Attempting to open a database connection...");

		try{
			SqlConnection connection = new(ConnectionString);
			connection.Open();

			_logger?.LogInformation(LogCategory.Database, "Database connection established successfully.");
			return connection;
		}
		catch (SqlException ex){
			_logger?.LogError(LogCategory.Database, "Failed to connect to database", ex);
			throw new CaeriusNetSqlException("Failed to open database connection : ", ex);
		}
	}
}