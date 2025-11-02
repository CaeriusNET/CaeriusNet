namespace CaeriusNet.Mappers;

/// <summary>
///     Represents the parameters to be passed to a stored procedure, including the procedure name,
///     capacity, list of parameters, and optional caching details.
/// </summary>
public sealed record StoredProcedureParameters
{
	private readonly SqlParameter[] _parameters;

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