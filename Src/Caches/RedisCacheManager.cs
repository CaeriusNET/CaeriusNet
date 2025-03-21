using CaeriusNet.Logging;

namespace CaeriusNet.Caches;

/// <summary>
///     Fournit des méthodes pour gérer un cache distribué basé sur Redis.
/// </summary>
internal static class RedisCacheManager
{
	private static ConnectionMultiplexer? _connection;
	private static IDatabase? _database;
	private static bool _isInitialized;
	private static readonly ICaeriusLogger? Logger = LoggerProvider.GetLogger();

	/// <summary>
	///     Initialise le gestionnaire de cache Redis avec la chaîne de connexion fournie.
	/// </summary>
	/// <param name="connectionString">La chaîne de connexion au serveur Redis.</param>
	/// <returns>True si l'initialisation a réussi, sinon False.</returns>
	internal static void Initialize(string connectionString)
	{
		if (_isInitialized)
		{
			Logger?.LogDebug(LogCategory.Redis,
				"Le gestionnaire Redis est déjà initialisé. Ignorer la réinitialisation.");
			return;
		}

		Logger?.LogDebug(LogCategory.Redis, "Tentative de connexion au serveur Redis...");

		try
		{
			_connection = ConnectionMultiplexer.Connect(connectionString);
			_database = _connection.GetDatabase();
			_isInitialized = true;
			Logger?.LogInformation(LogCategory.Redis, "Connexion au serveur Redis établie avec succès.");
		}
		catch (Exception ex)
		{
			Logger?.LogError(LogCategory.Redis, "Échec de la connexion au serveur Redis", ex);
			_connection?.Dispose();
			_connection = null;
			_database = null;
			_isInitialized = false;
		}
	}

	/// <summary>
	///     Vérifie si le gestionnaire de cache Redis est initialisé.
	/// </summary>
	/// <returns>True si le gestionnaire est initialisé, sinon False.</returns>
	internal static bool IsInitialized()
	{
		return _isInitialized;
	}

	/// <summary>
	///     Stocke une valeur dans le cache Redis.
	/// </summary>
	/// <typeparam name="T">Le type de la valeur à stocker dans le cache.</typeparam>
	/// <param name="cacheKey">La clé unique pour identifier la valeur dans le cache.</param>
	/// <param name="value">La valeur à stocker dans le cache.</param>
	/// <param name="expiration">La durée de validité de la valeur dans le cache avant son expiration.</param>
	/// <returns>True si le stockage a réussi, sinon False.</returns>
	internal static bool Store<T>(string cacheKey, T value, TimeSpan? expiration)
	{
		if (!_isInitialized || _database == null)
		{
			Logger?.LogWarning(LogCategory.Redis,
				$"Tentative d'enregistrement dans Redis avec la clé '{cacheKey}' alors que Redis n'est pas initialisé.");
			return false;
		}

		try
		{
			Logger?.LogDebug(LogCategory.Redis, $"Enregistrement dans Redis avec la clé '{cacheKey}'...");
			var serialized = JsonSerializer.Serialize(value);
			var result = _database.StringSet(cacheKey, serialized, expiration);

			if (result)
				Logger?.LogInformation(LogCategory.Redis,
					$"Valeur enregistrée dans Redis avec la clé '{cacheKey}' et l'expiration {(expiration.HasValue ? expiration.Value.ToString() : "illimitée")}");
			else
				Logger?.LogWarning(LogCategory.Redis, $"Échec de l'enregistrement dans Redis avec la clé '{cacheKey}'");

			return result;
		}
		catch (Exception ex)
		{
			Logger?.LogError(LogCategory.Redis, $"Erreur lors de l'enregistrement dans Redis avec la clé '{cacheKey}'",
				ex);
			return false;
		}
	}

	/// <summary>
	///     Tente de récupérer une valeur mise en cache de Redis.
	/// </summary>
	/// <typeparam name="T">Le type de valeur attendu du cache.</typeparam>
	/// <param name="cacheKey">La clé unique associée à la valeur dans le cache.</param>
	/// <param name="value">Le paramètre de sortie où la valeur mise en cache sera stockée si elle est trouvée.</param>
	/// <returns>
	///     True si la valeur est trouvée dans le cache et que son type correspond au type spécifié <typeparamref name="T" />;
	///     sinon, False.
	/// </returns>
	internal static bool TryGet<T>(string cacheKey, out T? value)
	{
		value = default;
		if (!_isInitialized || _database == null)
		{
			Logger?.LogWarning(LogCategory.Redis,
				$"Tentative de récupération depuis Redis avec la clé '{cacheKey}' alors que Redis n'est pas initialisé.");
			return false;
		}

		try
		{
			Logger?.LogDebug(LogCategory.Redis, $"Récupération depuis Redis avec la clé '{cacheKey}'...");
			var cached = _database.StringGet(cacheKey);

			if (cached.IsNull)
			{
				Logger?.LogDebug(LogCategory.Redis, $"Aucune valeur trouvée dans Redis pour la clé '{cacheKey}'");
				return false;
			}

			value = JsonSerializer.Deserialize<T>(cached!);
			var success = value != null;

			if (success)
				Logger?.LogInformation(LogCategory.Redis,
					$"Valeur récupérée avec succès depuis Redis pour la clé '{cacheKey}'");
			else
				Logger?.LogWarning(LogCategory.Redis, $"La désérialisation a échoué pour la clé '{cacheKey}'");

			return success;
		}
		catch (Exception ex)
		{
			Logger?.LogError(LogCategory.Redis, $"Erreur lors de la récupération depuis Redis avec la clé '{cacheKey}'",
				ex);
			return false;
		}
	}

	/// <summary>
	///     Libère les ressources utilisées par le gestionnaire de cache Redis.
	/// </summary>
	internal static void Dispose()
	{
		if (_connection != null)
			Logger?.LogInformation(LogCategory.Redis, "Fermeture de la connexion Redis");

		_connection?.Dispose();
		_connection = null;
		_database = null;
		_isInitialized = false;
	}
}