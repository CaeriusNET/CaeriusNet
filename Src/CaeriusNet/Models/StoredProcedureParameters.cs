namespace CaeriusNet.Models;

/// <summary>
///     Represents the parameters to be passed to a stored procedure, including the procedure name,
///     capacity, list of parameters.
/// </summary>
public sealed record StoredProcedureParameters(
    string ProcedureName,
    int Capacity,
    List<SqlParameter> Parameters,
    CacheType? CacheType,
    string? CacheKey,
    TimeSpan? CacheExpiration);