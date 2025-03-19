namespace CaeriusNet.Mappers;

/// <summary>
///     Represents the parameters to be passed to a stored procedure, including the procedure name,
///     capacity, list of parameters, and optional caching details.
/// </summary>
public sealed record StoredProcedureParameters(
	string ProcedureName,
	int Capacity,
	List<SqlParameter> Parameters,
	string? CacheKey,
	TimeSpan? CacheExpiration,
	CacheType? CacheType);