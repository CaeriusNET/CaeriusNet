namespace CaeriusNet.Mappers;

/// <summary>
///     Represents the parameters to be passed to a stored procedure, including the procedure name,
///     capacity, list of parameters, and optional caching details.
/// </summary>
public sealed record StoredProcedureParameters
{

	private readonly SqlParameter[] _parameters;

	/// <summary>
	///     Initializes a new instance of the <see cref="StoredProcedureParameters" /> class.
	/// </summary>
	/// <param name="schemaName">The schema name for the stored procedure.</param>
	/// <param name="procedureName">The name of the stored procedure to execute.</param>
	/// <param name="capacity">The capacity for the stored procedure execution.</param>
	/// <param name="parameters">The SQL parameters to pass to the stored procedure.</param>
	/// <param name="cacheKey">Optional cache key for storing results.</param>
	/// <param name="cacheExpiration">Optional timespan for cache expiration.</param>
	/// <param name="cacheType">Optional type of caching to use.</param>
	public StoredProcedureParameters(
		string schemaName,
		string procedureName,
		int capacity,
		ReadOnlyMemory<SqlParameter> parameters,
		string? cacheKey,
		TimeSpan? cacheExpiration,
		CacheType? cacheType)
	{
		SchemaName = schemaName;
		ProcedureName = procedureName;
		Capacity = capacity;
		_parameters = parameters.IsEmpty ? [] : parameters.ToArray();
		CacheKey = cacheKey;
		CacheExpiration = cacheExpiration;
		CacheType = cacheType;
	}
	public string SchemaName { get; }
	public string ProcedureName { get; }
	public int Capacity { get; }
	public string? CacheKey { get; }
	public TimeSpan? CacheExpiration { get; }
	public CacheType? CacheType { get; }

	/// <summary>
	///     Gets parameters as ReadOnlySpan for zero-copy access
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ReadOnlySpan<SqlParameter> GetParametersSpan()
	{
		return _parameters;
	}
}