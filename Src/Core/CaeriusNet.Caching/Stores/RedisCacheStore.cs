namespace CaeriusNet.Core.Caching.Stores;

/// <summary>
///     Internal Redis-backed cache adapter for CaeriusNet caching.
///     Resolves a shared <see cref="IRedisConnectionProvider" /> from DI and prepares a durable <see cref="IDatabase" />.
///     JSON payloads are serialized as UTF-8 to minimize allocations and avoid string conversions.
/// </summary>
/// <remarks>
///     Design principles:
///     - No public Redis primitives are exposed to callers.
///     - Initialization occurs through an application-provided <see cref="IServiceProvider" />.
///     - Store/TryGet are synchronous to match other stores; failures are contained within cache paths.
///     - Low allocation hot-path: use UTF-8 bytes, avoid LINQ/closures, and prefer fast checks.
/// </remarks>
internal static class RedisCacheStore
{
    private static volatile IServiceProvider? _serviceProvider;
    private static volatile IDatabase? _db;
    private static Task? _initTask;
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    ///     Wires the application's root <see cref="IServiceProvider" /> to the Redis cache store.
    ///     This method is expected to be called once during application startup.
    /// </summary>
    /// <param name="serviceProvider">The application's service provider.</param>
    public static void SetServiceProvider(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;

        // Kick off an initialization task that resolves the provider and caches the database proxy.
        // We intentionally avoid throwing here; readiness is checked via IsInitialized().
        _initTask = InitializeAsync(CancellationToken.None);
    }

    /// <summary>
    ///     Returns whether the Redis store is ready for use (database resolved and cached).
    /// </summary>
    public static bool IsInitialized()
    {
        return _db is not null;
    }

    /// <summary>
    ///     Stores a value serialized as JSON into Redis under the provided key.
    ///     If the store is not initialized, this method is a no-op.
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="cacheKey">Unique cache key.</param>
    /// <param name="value">Value to store.</param>
    /// <param name="expiration">Optional TTL; when null, the key does not expire.</param>
    public static void Store<T>(string cacheKey, T value, TimeSpan? expiration)
    {
        var db = _db;
        if (db is null || string.IsNullOrEmpty(cacheKey))
            return;

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(value, _serializerOptions);
            if (expiration is { } ttl)
                db.StringSet(cacheKey, payload, ttl, When.Always, CommandFlags.FireAndForget);
            else
                db.StringSet(cacheKey, payload, null, When.Always, CommandFlags.FireAndForget);
        }
        catch
        {
            // Cache path is non-authoritative; swallow to preserve primary flow.
        }
    }

    /// <summary>
    ///     Attempts to retrieve and deserialize a JSON value from Redis for the provided key.
    /// </summary>
    /// <typeparam name="T">Expected value type.</typeparam>
    /// <param name="cacheKey">Unique cache key.</param>
    /// <param name="value">Result if found and successfully deserialized; otherwise default.</param>
    /// <returns>true if a value was found and deserialized; otherwise false.</returns>
    public static bool TryGet<T>(string cacheKey, out T? value)
    {
        var db = _db;
        if (db is null || string.IsNullOrEmpty(cacheKey))
        {
            value = default;
            return false;
        }

        try
        {
            var rv = db.StringGet(cacheKey);
            if (!rv.HasValue || rv.IsNullOrEmpty)
            {
                value = default;
                return false;
            }

            ReadOnlyMemory<byte> bytes = rv;
            value = JsonSerializer.Deserialize<T>(bytes.Span, _serializerOptions);
            return value is not null;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    private static async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var sp = _serviceProvider;
            if (sp is null) return;

            // Resolve provider from DI and prepare a durable database proxy using the provider default DB.
            var provider = sp.GetService(typeof(IRedisConnectionProvider)) as IRedisConnectionProvider;
            if (provider is null) return;

            await provider.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            var database = await provider.GetDatabaseAsync(null, cancellationToken).ConfigureAwait(false);

            _db = database;
        }
        catch
        {
            // Leave uninitialized; callers will detect via IsInitialized().
        }
    }
}