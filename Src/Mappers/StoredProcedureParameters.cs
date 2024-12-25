namespace CaeriusNet.Mappers;

/// <summary>
///     Represents the parameters to be passed to a stored procedure.
/// </summary>
public sealed record StoredProcedureParameters(
    string ProcedureName,
    int Capacity,
    List<SqlParameter> Parameters,
    string? CacheKey,
    TimeSpan? CacheExpiration,
    CacheType? CacheType);