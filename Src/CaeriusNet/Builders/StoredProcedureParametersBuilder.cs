namespace CaeriusNet.Builders;

/// <summary>
///     Provides functionality to build parameters for a stored procedure call, including support for regular,
///     Table-Valued Parameters (TVPs), and caching mechanisms.
/// </summary>
public sealed record StoredProcedureParametersBuilder(string ProcedureName, int Capacity = 1)
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
    /// <param name="dbType">The TSQL data type of the parameter. Use <see cref="SqlDbType" /> enumeration.</param>
    /// <returns>The <see cref="StoredProcedureParametersBuilder" /> instance for chaining.</returns>
    public StoredProcedureParametersBuilder AddParameter(string parameter, object value, SqlDbType dbType)
    {
        var currentItemParameter = new SqlParameter(parameter, dbType) { Value = value };
        Parameters.Add(currentItemParameter);
        return this;
    }

    /// <summary>
    ///     Adds a Table-Valued Parameter (TVP) to the stored procedure call.
    /// </summary>
    /// <typeparam name="T">The type of the object that maps to the TVP.</typeparam>
    /// <param name="parameter">The name of the TVP parameter.</param>
    /// <param name="tvpName">The name of the TVP type in SQL Server.</param>
    /// <param name="items">The collection of items to map to the TVP using the ITvpMapper interface.</param>
    /// <returns>The StoredProcedureParametersBuilder instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the items collection is empty.</exception>
    public StoredProcedureParametersBuilder AddTvpParameter<T>(string parameter, string tvpName, IEnumerable<T> items)
        where T : class, ITvpMapper<T>
    {
        var tvpMappers = items.ToList();
        if (tvpMappers.Count == 0)
            throw new ArgumentException("No items found in the collection to map to a Table-Valued Parameter.");

        var dataTable = tvpMappers[0].MapAsDataTable(tvpMappers);
        var currentTvpParameter = new SqlParameter(parameter, SqlDbType.Structured)
        {
            TypeName = tvpName,
            Value = dataTable
        };

        Parameters.Add(currentTvpParameter);
        return this;
    }

    /// <summary>
    ///     Adds caching support to the stored procedure call.
    /// </summary>
    /// <param name="cacheKey">The unique key for the cache.</param>
    /// <param name="expiration">Optional expiration time for the cache. Defaults to null for no expiration.</param>
    /// <param name="cacheType">The type of cache strategy to use. Defaults to <see cref="CacheType.InMemory" />.</param>
    /// <returns>The <see cref="StoredProcedureParametersBuilder" /> instance for chaining.</returns>
    private StoredProcedureParametersBuilder AddCache(string cacheKey, TimeSpan? expiration = null,
        CacheType cacheType = CacheType.InMemory)
    {
        _cacheType = cacheType;
        _cacheKey = cacheKey;
        _cacheExpiration = expiration;
        return this;
    }

    /// <summary>
    ///     Configures in-memory caching with key and expiration.
    /// </summary>
    public StoredProcedureParametersBuilder UseInMemoryCache(string cacheKey, TimeSpan expiration)
    {
        return AddCache(cacheKey, expiration);
    }

    /// <summary>
    ///     Configures frozen snapshot caching (read-only, no expiration).
    /// </summary>
    public StoredProcedureParametersBuilder UseFrozenCache(string cacheKey)
    {
        return AddCache(cacheKey, null, CacheType.Frozen);
    }

    /// <summary>
    ///     Configures Redis distributed caching with an optional expiration.
    /// </summary>
    public StoredProcedureParametersBuilder UseRedisCache(string cacheKey, TimeSpan? expiration = null)
    {
        return AddCache(cacheKey, expiration, CacheType.Redis);
    }

    /// <summary>
    ///     Builds an immutable <see cref="StoredProcedureParameters" /> instance carrying cache configuration.
    /// </summary>
    public StoredProcedureParameters Build()
    {
        return new StoredProcedureParameters(ProcedureName, Capacity, Parameters, _cacheType, _cacheKey,
            _cacheExpiration);
    }
}