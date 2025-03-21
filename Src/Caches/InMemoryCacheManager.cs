using CaeriusNet.Logging;

namespace CaeriusNet.Caches;

/// <summary>
///     Represents a utility class that manages caching operations in memory,
///     such as storing and retrieving cached data.
/// </summary>
internal static class InMemoryCacheManager
{
	private static readonly MemoryCache MemoryCache = new(new MemoryCacheOptions());
	private static readonly ICaeriusLogger? Logger = LoggerProvider.GetLogger();

	/// <summary>
	///     Stores the specified value in the in-memory cache with the given cache key and expiration time.
	/// </summary>
	/// <typeparam name="T">The type of the value to be stored in the cache.</typeparam>
	/// <param name="cacheKey">The unique key used to store and retrieve the value from the cache.</param>
	/// <param name="value">The value to be stored in the cache.</param>
	/// <param name="expiration">The duration for which the cached value is valid before it expires.</param>
	internal static void Store<T>(string cacheKey, T value, TimeSpan expiration)
	{
		Logger?.LogDebug(LogCategory.InMemoryCache,
			$"Enregistrement dans le cache mémoire avec la clé '{cacheKey}'...");
		MemoryCache.Set(cacheKey, value!, expiration);
		Logger?.LogInformation(LogCategory.InMemoryCache,
			$"Valeur enregistrée dans le cache mémoire avec la clé '{cacheKey}' et l'expiration de {expiration}");
	}

	/// <summary>
	///     Attempts to retrieve a cached value from the in-memory cache based on the specified cache key.
	/// </summary>
	/// <typeparam name="T">The type of value expected to be retrieved from the cache.</typeparam>
	/// <param name="cacheKey">The unique identifier for the cached item.</param>
	/// <param name="value">
	///     An output parameter that, upon completion, will contain the retrieved value if found and of the correct type;
	///     otherwise, contains the default value of the type.
	/// </param>
	/// <returns>
	///     True if the cache contains an item with the specified key and the value is of the expected type; otherwise, false.
	/// </returns>
	internal static bool TryGet<T>(string cacheKey, out T? value)
	{
		Logger?.LogDebug(LogCategory.InMemoryCache,
			$"Récupération depuis le cache mémoire avec la clé '{cacheKey}'...");

		if (MemoryCache.TryGetValue(cacheKey, out var cached) && cached is T typedValue)
		{
			value = typedValue;
			Logger?.LogInformation(LogCategory.InMemoryCache,
				$"Valeur récupérée avec succès depuis le cache mémoire pour la clé '{cacheKey}'");
			return true;
		}

		value = default;
		Logger?.LogDebug(LogCategory.InMemoryCache,
			$"Aucune valeur trouvée dans le cache mémoire pour la clé '{cacheKey}'");
		return false;
	}
}