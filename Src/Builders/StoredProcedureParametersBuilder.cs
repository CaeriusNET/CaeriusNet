namespace CaeriusNet.Builders;

/// <summary>
///     A builder class for configuring and creating stored procedure parameter collections.
/// </summary>
/// <remarks>
///     The <see cref="StoredProcedureParametersBuilder" /> class provides a fluent API for building
///     parameters for stored procedure execution. It supports regular SQL parameters, Table-Valued
///     Parameters (TVPs), and multiple caching mechanisms.
/// </remarks>
/// <param name="ProcedureName">The name of the stored procedure to execute.</param>
/// <param name="ResultSetCapacity">
///     Expected number of rows in the result set (not parameters).
///     Used for pre-allocating collections to avoid resizing.
///     Default is 1 for single-row results.
/// </param>
public sealed record StoredProcedureParametersBuilder(string ProcedureName, int ResultSetCapacity = 1)
{
	private TimeSpan? _cacheExpiration;
	private string? _cacheKey;
	private CacheType? _cacheType;

	/// <summary>
	///     Gets the collection of SQL parameters to be used in the stored procedure call.
	/// </summary>
	private List<SqlParameter> Parameters { get; } = [];

	/// <summary>
	///     Adds a parameter to the stored procedure call.
	/// </summary>
	/// <param name="parameter">The name of the parameter.</param>
	/// <param name="value">The value of the parameter.</param>
	/// <param name="dbType">The SQL Server data type of the parameter.</param>
	/// <returns>The current builder instance to enable method chaining.</returns>
	/// <exception cref="ArgumentNullException">
	///     <paramref name="parameter" /> is null.
	/// </exception>
	public StoredProcedureParametersBuilder AddParameter(string parameter, object value, SqlDbType dbType)
	{
		Parameters.Add(new SqlParameter(parameter, dbType) { Value = value });
		return this;
	}

	/// <summary>
	///     Adds a Table-Valued Parameter (TVP) to the stored procedure call.
	/// </summary>
	/// <typeparam name="T">The type of objects in the TVP that implement <see cref="ITvpMapper{T}" />.</typeparam>
	/// <param name="parameter">The name of the TVP parameter.</param>
	/// <param name="items">The collection of items to include in the TVP.</param>
	/// <returns>The current builder instance to enable method chaining.</returns>
	/// <exception cref="ArgumentException">The items collection is empty.</exception>
	/// <exception cref="ArgumentNullException">
	///     <paramref name="parameter" /> or <paramref name="items" /> is null.
	/// </exception>
	public StoredProcedureParametersBuilder AddTvpParameter<T>(string parameter, IEnumerable<T> items)
		where T : class, ITvpMapper<T>
	{
		var tvpMappers = items.ToList();
		if (tvpMappers.Count == 0)
			throw new ArgumentException("No items found in the collection to map to a Table-Valued Parameter.");

		var dataTable = tvpMappers[0].MapAsDataTable(tvpMappers);
		var currentTvpParameter = new SqlParameter(parameter, SqlDbType.Structured)
		{
			TypeName = T.TvpTypeName,
			Value = dataTable
		};

		Parameters.Add(currentTvpParameter);
		return this;
	}

	/// <summary>
	///     Adds caching support to the stored procedure call.
	/// </summary>
	/// <param name="cacheKey">The unique key for the cache entry.</param>
	/// <param name="expiration">Optional time span after which the cache entry expires.</param>
	/// <param name="cacheType">The type of caching to use.</param>
	/// <returns>The current builder instance to enable method chaining.</returns>
	/// <exception cref="ArgumentNullException">
	///     <paramref name="cacheKey" /> is null.
	/// </exception>
	public StoredProcedureParametersBuilder AddCache(string cacheKey, TimeSpan? expiration = null,
		CacheType cacheType = InMemory)
	{
		_cacheType = cacheType;
		_cacheKey = cacheKey;
		_cacheExpiration = expiration;
		return this;
	}

	/// <summary>
	///     Configures the stored procedure to use in-memory caching.
	/// </summary>
	/// <param name="cacheKey">The unique key for the cache entry.</param>
	/// <param name="expiration">The time span after which the cache entry expires.</param>
	/// <returns>The current builder instance to enable method chaining.</returns>
	/// <exception cref="ArgumentNullException">
	///     <paramref name="cacheKey" /> is null.
	/// </exception>
	public StoredProcedureParametersBuilder AddInMemoryCache(string cacheKey, TimeSpan expiration)
	{
		_cacheKey = cacheKey;
		_cacheType = InMemory;
		_cacheExpiration = expiration;
		return this;
	}

	/// <summary>
	///     Configures the stored procedure to use frozen (immutable) caching.
	/// </summary>
	/// <param name="cacheKey">The unique key for the frozen cache entry.</param>
	/// <returns>The current builder instance to enable method chaining.</returns>
	/// <exception cref="ArgumentNullException">
	///     <paramref name="cacheKey" /> is null.
	/// </exception>
	public StoredProcedureParametersBuilder AddFrozenCache(string cacheKey)
	{
		_cacheKey = cacheKey;
		_cacheType = Frozen;
		_cacheExpiration = null;
		return this;
	}

	/// <summary>
	///     Configures the stored procedure to use Redis distributed caching.
	/// </summary>
	/// <param name="cacheKey">The unique key for the Redis cache entry.</param>
	/// <param name="expiration">Optional time span after which the Redis cache entry expires.</param>
	/// <returns>The current builder instance to enable method chaining.</returns>
	/// <exception cref="ArgumentNullException">
	///     <paramref name="cacheKey" /> is null.
	/// </exception>
	public StoredProcedureParametersBuilder AddRedisCache(string cacheKey, TimeSpan? expiration = null)
	{
		_cacheType = Redis;
		_cacheKey = cacheKey;
		_cacheExpiration = expiration;
		return this;
	}

	/// <summary>
	///     Creates a new <see cref="StoredProcedureParameters" /> instance with the configured settings.
	/// </summary>
	/// <returns>A new <see cref="StoredProcedureParameters" /> object containing all specified parameters and settings.</returns>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public StoredProcedureParameters Build()
	{
		var parametersMemory = Parameters.Count > 0
			? CollectionsMarshal.AsSpan(Parameters).ToArray().AsMemory()
			: ReadOnlyMemory<SqlParameter>.Empty;

		return new StoredProcedureParameters(
		ProcedureName,
		ResultSetCapacity,
		parametersMemory,
		_cacheKey,
		_cacheExpiration,
		_cacheType);
	}
}