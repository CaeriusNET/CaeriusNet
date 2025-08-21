using CaeriusNet.Extensions.Caching.Types.Enums;
using Microsoft.Data.SqlClient;

namespace CaeriusNet.Extensions.Caching.Mappers;

/// <summary>
///     Represents the parameters to be passed to a stored procedure, including the procedure name,
///     capacity, list of parameters, and optional caching details.
/// </summary>
public sealed record StoredProcedureParameters(
	string ProcedureName,
	int Capacity,
	List<SqlParameter> Parameters,
	CacheType? CacheType,
	string? CacheKey,
	TimeSpan? CacheExpiration);