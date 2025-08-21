using CaeriusNet.Extensions.Caching.Mappers;
using CaeriusNet.Extensions.Caching.Types.Enums;
using Microsoft.Data.SqlClient;

namespace CaeriusNet.Extensions.Caching.Builders;

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
	///     Adds caching support to the stored procedure call.
	/// </summary>
	/// <param name="cacheKey">The unique key for the cache.</param>
	/// <param name="expiration">Optional expiration time for the cache. Defaults to null for no expiration.</param>
	/// <param name="cacheType">The type of cache strategy to use. Defaults to <see cref="CacheType.InMemory" />.</param>
	/// <returns>The <see cref="StoredProcedureParametersBuilder" /> instance for chaining.</returns>
	public StoredProcedureParametersBuilder AddCache(string cacheKey, TimeSpan? expiration = null,
		CacheType cacheType = CacheType.InMemory)
	{
		_cacheType = cacheType;
		_cacheKey = cacheKey;
		_cacheExpiration = expiration;
		return this;
	}

	/// <summary>
	///     Configures the stored procedure parameters to use in-memory caching with a specified key and expiration time.
	/// </summary>
	/// <param name="cacheKey">The unique key used to identify the cache entry.</param>
	/// <param name="expiration">The duration after which the cache entry will expire.</param>
	/// <returns>The <see cref="StoredProcedureParametersBuilder" /> instance for chaining.</returns>
	public StoredProcedureParametersBuilder AddInMemoryCache(string cacheKey, TimeSpan expiration)
	{
		_cacheKey = cacheKey;
		_cacheType = CacheType.InMemory;
		_cacheExpiration = expiration;
		return this;
	}

	/// <summary>
	///     Adds a frozen cache to the stored procedure call.
	/// </summary>
	/// <param name="cacheKey">The unique key used to identify the frozen cache.</param>
	/// <returns>The <see cref="StoredProcedureParametersBuilder" /> instance for chaining.</returns>
	public StoredProcedureParametersBuilder AddFrozenCache(string cacheKey)
	{
		_cacheKey = cacheKey;
		_cacheType = CacheType.Frozen;
		_cacheExpiration = null;
		return this;
	}

	/// <summary>
	///     Adds Redis cache support for the stored procedure call, allowing caching of the results.
	/// </summary>
	/// <param name="cacheKey">
	///     The unique key used to store and access the cached result in Redis.
	/// </param>
	/// <param name="expiration">
	///     The expiration time of the cached data. This value is optional, and a default expiration may be used
	///     if not specified.
	/// </param>
	/// <returns>
	///     The <see cref="StoredProcedureParametersBuilder" /> instance for chaining.
	/// </returns>
	public StoredProcedureParametersBuilder AddRedisCache(string cacheKey, TimeSpan? expiration = null)
	{
		_cacheType = CacheType.Redis;
		_cacheKey = cacheKey;
		_cacheExpiration = expiration;
		return this;
	}

	/// <summary>
	///     Builds and returns a <see cref="StoredProcedureParameters" /> object containing all configured parameters.
	/// </summary>
	/// <returns>
	///     A <see cref="StoredProcedureParameters" /> instance containing the stored procedure name, capacity,
	///     parameters, and optional caching settings.
	/// </returns>
	public StoredProcedureParameters Build()
	{
		return new StoredProcedureParameters(ProcedureName, Capacity, Parameters, _cacheType, _cacheKey, _cacheExpiration);
	}
}