using CaeriusNet.Logging;

namespace CaeriusNet.Factories;

/// <summary>
///     Represents a factory for creating and managing database connections based on a provided connection string.
/// </summary>
internal sealed record CaeriusDbContext(string ConnectionString) : ICaeriusDbContext
{
	private readonly ICaeriusLogger? _logger = LoggerProvider.GetLogger();

	/// <summary>
	///     Creates and opens a database connection.
	/// </summary>
	/// <returns>An open <see cref="IDbConnection" />.</returns>
	/// <exception cref="CaeriusSqlException">Thrown when the connection fails to open.</exception>
	public IDbConnection DbConnection()
	{
		_logger?.LogDebug(LogCategory.Database, "Tentative d'ouverture d'une connexion à la base de données...");

		try
		{
			SqlConnection connection = new(ConnectionString);
			connection.Open();

			_logger?.LogInformation(LogCategory.Database, "Connexion à la base de données établie avec succès.");
			return connection;
		}
		catch (SqlException ex)
		{
			_logger?.LogError(LogCategory.Database, "Échec de la connexion à la base de données", ex);
			throw new CaeriusSqlException("Failed to open database connection : ", ex);
		}
	}
}