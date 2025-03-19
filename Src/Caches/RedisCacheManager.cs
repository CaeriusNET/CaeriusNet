namespace CaeriusNet.Caches;

/// <summary>
///     Fournit des méthodes pour gérer un cache distribué basé sur Redis.
/// </summary>
internal static class RedisCacheManager
{
	private static ConnectionMultiplexer? _connection;
	private static IDatabase? _database;
	private static bool _isInitialized;

	/// <summary>
	///     Initialise le gestionnaire de cache Redis avec la chaîne de connexion fournie.
	/// </summary>
	/// <param name="connectionString">La chaîne de connexion au serveur Redis.</param>
	/// <returns>True si l'initialisation a réussi, sinon False.</returns>
	internal static void Initialize(string connectionString)
	{
		if (_isInitialized) return;

		try
		{
			_connection = ConnectionMultiplexer.Connect(connectionString);
			_database = _connection.GetDatabase();
			_isInitialized = true;
		}
		catch
		{
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
		if (!_isInitialized || _database == null) return false;

		try
		{
			var serialized = JsonSerializer.Serialize(value);
			return _database.StringSet(cacheKey, serialized, expiration);
		}
		catch
		{
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
		if (!_isInitialized || _database == null) return false;

		try
		{
			var cached = _database.StringGet(cacheKey);
			if (cached.IsNull) return false;

			value = JsonSerializer.Deserialize<T>(cached!);
			return value != null;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	///     Libère les ressources utilisées par le gestionnaire de cache Redis.
	/// </summary>
	internal static void Dispose()
	{
		_connection?.Dispose();
		_connection = null;
		_database = null;
		_isInitialized = false;
	}
}