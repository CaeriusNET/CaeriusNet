using CaeriusNet.Logging;

namespace CaeriusNet.Caches;

/// <summary>
///     Provides methods for managing an immutable, thread-safe cache using frozen dictionaries.
/// </summary>
internal static class FrozenCacheManager
{
	private static volatile FrozenDictionary<string, object> _frozenCache = FrozenDictionary<string, object>.Empty;
	private static readonly object Lock = new();
	private static readonly ICaeriusLogger? Logger = LoggerProvider.GetLogger();

	/// <summary>
	///     Stores a value in the frozen dictionary-based cache if it is not already present.
	/// </summary>
	/// <typeparam name="T">The type of the value to be stored in the cache.</typeparam>
	/// <param name="cacheKey">The unique key to associate with the value in the cache.</param>
	/// <param name="value">The value to be stored in the cache.</param>
	internal static void StoreFrozen<T>(string cacheKey, T value)
	{
		Logger?.LogDebug(LogCategory.FrozenCache,
			$"Tentative d'enregistrement dans le cache gelé avec la clé '{cacheKey}'...");

		lock (Lock)
		{
			if (_frozenCache.ContainsKey(cacheKey))
			{
				Logger?.LogDebug(LogCategory.FrozenCache,
					$"La clé '{cacheKey}' existe déjà dans le cache gelé. Ignorer l'enregistrement.");
				return;
			}

			var mutableCache = new ConcurrentDictionary<string, object>(_frozenCache) { [cacheKey] = value! };
			_frozenCache = mutableCache.ToFrozenDictionary();
			Logger?.LogInformation(LogCategory.FrozenCache,
				$"Valeur enregistrée dans le cache gelé avec la clé '{cacheKey}'");
		}
	}

	/// <summary>
	///     Attempts to retrieve a value from the frozen dictionary-based cache.
	/// </summary>
	/// <typeparam name="T">The expected type of the cached value.</typeparam>
	/// <param name="cacheKey">The unique key associated with the value in the cache.</param>
	/// <param name="value">The output parameter where the cached value will be stored if found.</param>
	/// <returns>
	///     true if the value is found in the cache and its type matches the specified type <typeparamref name="T" />;
	///     otherwise, false.
	/// </returns>
	internal static bool TryGetFrozen<T>(string cacheKey, out T? value)
	{
		Logger?.LogDebug(LogCategory.FrozenCache, $"Récupération depuis le cache gelé avec la clé '{cacheKey}'...");

		if (_frozenCache.TryGetValue(cacheKey, out var cached) && cached is T typedValue)
		{
			value = typedValue;
			Logger?.LogInformation(LogCategory.FrozenCache,
				$"Valeur récupérée avec succès depuis le cache gelé pour la clé '{cacheKey}'");
			return true;
		}

		value = default;
		Logger?.LogDebug(LogCategory.FrozenCache, $"Aucune valeur trouvée dans le cache gelé pour la clé '{cacheKey}'");
		return false;
	}
}