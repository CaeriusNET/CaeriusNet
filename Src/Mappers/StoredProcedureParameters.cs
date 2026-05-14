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
    /// <param name="commandTimeout">
    ///     The wait time in seconds before terminating the command and raising an error. Defaults to 30.
    /// </param>
    public StoredProcedureParameters(
        string schemaName,
        string procedureName,
        int capacity,
        SqlParameter[] parameters,
        string? cacheKey,
        TimeSpan? cacheExpiration,
        CacheType? cacheType,
        int commandTimeout = 30)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        ArgumentOutOfRangeException.ThrowIfNegative(commandTimeout);

        SchemaName = schemaName;
        ProcedureName = procedureName;
        Capacity = capacity;
        _parameters = CloneParameters(parameters);
        CacheKey = cacheKey;
        CacheExpiration = cacheExpiration;
        CacheType = cacheType;
        CommandTimeout = commandTimeout;
    }

    public string SchemaName { get; }
    public string ProcedureName { get; }
    public int Capacity { get; }
    public string? CacheKey { get; }
    public TimeSpan? CacheExpiration { get; }
    public CacheType? CacheType { get; }

    /// <summary>
    ///     Gets the wait time in seconds before terminating the command and raising an error.
    /// </summary>
    public int CommandTimeout { get; }

    /// <summary>
    ///     Gets parameters as ReadOnlySpan for zero-copy access
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<SqlParameter> GetParametersSpan()
    {
        return _parameters;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal void AddParametersTo(SqlParameterCollection parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        for (var i = 0; i < _parameters.Length; i++)
            parameters.Add(CloneParameter(_parameters[i]));
    }

    private static SqlParameter[] CloneParameters(SqlParameter[] parameters)
    {
        if (parameters.Length == 0)
            return [];

        var clones = new SqlParameter[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
            clones[i] = CloneParameter(parameters[i]);

        return clones;
    }

    internal static SqlParameter CloneParameter(SqlParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        var clone = (SqlParameter)((ICloneable)parameter).Clone();
        clone.ParameterName = SqlParameterName.Normalize(clone.ParameterName);
        clone.Value ??= DBNull.Value;
        return clone;
    }
}

internal static class SqlParameterName
{
    internal static string Normalize(string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);

        if (parameterName.Length != parameterName.Trim().Length)
            throw new ArgumentException("SQL parameter names cannot contain leading or trailing whitespace.",
                nameof(parameterName));

        var normalized = parameterName[0] == '@'
            ? parameterName
            : "@" + parameterName;

        if (normalized.Length == 1)
            throw new ArgumentException("SQL parameter names must include a name after '@'.", nameof(parameterName));

        if (normalized[1] == '@')
            throw new ArgumentException("SQL parameter names must use a single leading '@'.", nameof(parameterName));

        for (var i = 0; i < normalized.Length; i++)
        {
            if (char.IsControl(normalized[i]))
                throw new ArgumentException("SQL parameter names cannot contain control characters.",
                    nameof(parameterName));
        }

        return normalized;
    }
}
