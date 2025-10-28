namespace CaeriusNet.Caches;

/// <summary>
///     Provides methods to manage Redis-based distributed cache.
/// </summary>
internal sealed class RedisCacheManager : IRedisCacheManager
{
	/// <summary>
	///     JSON serialization options used for cache operations.
	/// </summary>
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		PropertyNameCaseInsensitive = true,
		PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate
	};

	private readonly bool _isLoggingEnabled;
	private readonly ILogger<RedisCacheManager>? _logger;
	private readonly IRedisCacheConnection? _redisCacheConnection;

	/// <summary>
	///     Initializes a new instance of the <see cref="RedisCacheManager" /> class using dependency injection.
	/// </summary>
	/// <param name="redisCacheConnection">The Redis cache connection instance.</param>
	/// <param name="logger">Optional logger for Redis operations.</param>
	public RedisCacheManager(
		IRedisCacheConnection redisCacheConnection,
		ILogger<RedisCacheManager>? logger = null)
	{
		_redisCacheConnection = redisCacheConnection;
		_logger = logger;
		_isLoggingEnabled = logger != null;
		IsInitialized = _redisCacheConnection?.IsConnected() ?? false;

		if (IsInitialized && _isLoggingEnabled)
			_logger!.LogRedisConnected();
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="RedisCacheManager" /> class using a connection string.
	/// </summary>
	/// <param name="connectionString">The Redis connection string.</param>
	/// <param name="logger">Optional logger for Redis operations.</param>
	public RedisCacheManager(
		string connectionString,
		ILogger<RedisCacheManager>? logger = null)
	{
		_logger = logger;
		_isLoggingEnabled = logger != null;

		if (_isLoggingEnabled)
			_logger!.LogRedisConnecting();

		try{
			var multiplexer = ConnectionMultiplexer.Connect(connectionString);
			_redisCacheConnection = new RedisCacheConnection(multiplexer);
			IsInitialized = true;

			if (_isLoggingEnabled)
				_logger!.LogRedisConnected();
		}
		catch (Exception ex){
			if (_isLoggingEnabled)
				_logger!.LogRedisConnectionFailed(ex);
			IsInitialized = false;
		}
	}

	/// <summary>
	///     Gets a value indicating whether the Redis cache manager is initialized.
	/// </summary>
	public bool IsInitialized { get; }

	/// <summary>
	///     Stores a value in the Redis cache.
	/// </summary>
	/// <typeparam name="T">The type of value to store.</typeparam>
	/// <param name="cacheKey">The key under which to store the value.</param>
	/// <param name="value">The value to store.</param>
	/// <param name="expiration">Optional time span after which the value expires.</param>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public void Store<T>(string cacheKey, T value, TimeSpan? expiration)
	{
		if (!IsInitialized || _redisCacheConnection == null) return;

		try{
			var database = _redisCacheConnection.GetDatabase();

			var bufferWriter = new ArrayBufferWriter<byte>(1024);
			using (var writer = new Utf8JsonWriter(bufferWriter))
				JsonSerializer.Serialize(writer, value, JsonOptions);

			var serialized = bufferWriter.WrittenSpan;
			database.StringSet(cacheKey, serialized.ToArray(), expiration);
		}
		catch (Exception ex){
			if (_isLoggingEnabled) _logger!.LogRedisStoreError(cacheKey, ex);
		}
	}

	/// <summary>
	///     Attempts to retrieve a cached value from Redis.
	/// </summary>
	/// <typeparam name="T">The type of value to retrieve.</typeparam>
	/// <param name="cacheKey">The key of the value to retrieve.</param>
	/// <param name="value">When this method returns, contains the retrieved value if found; otherwise, the default value.</param>
	/// <returns>true if the value was found; otherwise, false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public bool TryGet<T>(string cacheKey, out T? value)
	{
		value = default;

		if (!IsInitialized || _redisCacheConnection == null) return false;

		try{
			var database = _redisCacheConnection.GetDatabase();
			var cached = database.StringGet(cacheKey);

			if (cached.IsNull) return false;

			ReadOnlySpan<byte> bytes = (byte[])cached!;
			value = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
			return value != null;
		}
		catch{ return false; }
	}
}