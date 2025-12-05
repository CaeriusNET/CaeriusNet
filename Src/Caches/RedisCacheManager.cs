namespace CaeriusNet.Caches;

/// <summary>
///     Provides Redis-based distributed cache operations using pure dependency injection.
/// </summary>
internal sealed class RedisCacheManager : IRedisCacheManager
{
	/// <summary>
	///     A thread-local pool of ArrayBufferWriter instances used for efficient byte buffer management.
	/// </summary>
	/// <remarks>
	///     Each thread maintains its own ArrayBufferWriter instance with an initial capacity of 4096 bytes.
	///     This helps avoid allocation overhead in high-concurrency scenarios.
	/// </remarks>
	private static readonly ThreadLocal<ArrayBufferWriter<byte>> BufferWriterPool =
        new(() => new ArrayBufferWriter<byte>(4096));

	/// <summary>
	///     JSON serialization options for Redis cache operations.
	/// </summary>
	/// <remarks>
	///     Configures serialization to ignore null values, use case-insensitive property names,
	///     and populate existing object instances during deserialization.
	/// </remarks>
	private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate
    };

	/// <summary>
	///     The distributed cache instance for Redis operations.
	/// </summary>
	private readonly IDistributedCache? _distributedCache;

	/// <summary>
	///     Logger instance for diagnostic and error reporting.
	/// </summary>
	private readonly ILogger? _logger;

	/// <summary>
	///     Initializes a new instance of the RedisCacheManager.
	/// </summary>
	public RedisCacheManager(IDistributedCache? distributedCache, ILoggerFactory? loggerFactory = null)
    {
        _distributedCache = distributedCache;
        _logger = loggerFactory?.CreateLogger<RedisCacheManager>();

        if (_distributedCache != null && _logger != null)
            _logger.LogRedisConnected();
    }

	/// <summary>
	///     Stores a value in the Redis cache.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Store<T>(string cacheKey, T value, TimeSpan? expiration) where T : notnull
    {
        if (_distributedCache == null) return;

        try
        {
            _logger?.LogStoringInRedis(cacheKey);

            var bufferWriter = BufferWriterPool.Value!;
            bufferWriter.Clear();

            using (var writer = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions
                   {
                       SkipValidation = true,
                       Indented = false
                   }))
            {
                JsonSerializer.Serialize(writer, value, JsonOptions);
            }

            var options = new DistributedCacheEntryOptions();
            if (expiration.HasValue)
                options.AbsoluteExpirationRelativeToNow = expiration.Value;

            var written = bufferWriter.WrittenSpan;
            _distributedCache.Set(cacheKey, written.ToArray(), options);
        }
        catch (Exception ex)
        {
            _logger?.LogRedisStoreError(cacheKey, ex);
        }
    }

	/// <summary>
	///     Attempts to retrieve a cached value from Redis.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryGet<T>(string cacheKey, out T? value)
    {
        value = default;
        if (_distributedCache == null) return false;

        try
        {
            var cached = _distributedCache.Get(cacheKey);
            if (cached == null || cached.Length == 0) return false;

            ReadOnlySpan<byte> bytes = cached;
            var reader = new Utf8JsonReader(bytes);
            value = JsonSerializer.Deserialize<T>(ref reader, JsonOptions);

            if (value != null && _logger != null)
                _logger.LogRetrievedFromRedis(cacheKey);

            return value != null;
        }
        catch
        {
            return false;
        }
    }
}