namespace CaeriusNet.Mappers;

/// <summary>
///     Typed wrapper over <see cref="StoredProcedureParameters" /> for generated procedure contracts.
/// </summary>
/// <typeparam name="TProcedure">The generated procedure descriptor type.</typeparam>
public sealed record StoredProcedureParameters<TProcedure>
    where TProcedure : struct, ICaeriusGeneratedProcedure<TProcedure>
{
    private readonly StoredProcedureParameters _parameters;

    /// <summary>
    ///     Initializes a new typed wrapper around untyped stored procedure parameters.
    /// </summary>
    /// <param name="parameters">The untyped parameters produced by the runtime builder.</param>
    public StoredProcedureParameters(StoredProcedureParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        _parameters = parameters;
    }

    /// <summary>Gets the SQL Server schema name.</summary>
    public string SchemaName => _parameters.SchemaName;

    /// <summary>Gets the SQL Server stored procedure name.</summary>
    public string ProcedureName => _parameters.ProcedureName;

    /// <summary>Gets the fully qualified SQL Server stored procedure name.</summary>
    public string FullName => TProcedure.FullName;

    /// <summary>Gets the stable contract hash from the generated manifest.</summary>
    public string ContractHash => TProcedure.ContractHash;

    /// <summary>Gets the generated procedure parameter count.</summary>
    public int ParameterCount => TProcedure.ParameterCount;

    /// <summary>Gets the generated procedure result-set count.</summary>
    public int ResultSetCount => TProcedure.ResultSetCount;

    /// <summary>Gets the expected result-set capacity.</summary>
    public int Capacity => _parameters.Capacity;

    /// <summary>Gets the optional cache key.</summary>
    public string? CacheKey => _parameters.CacheKey;

    /// <summary>Gets the optional cache expiration.</summary>
    public TimeSpan? CacheExpiration => _parameters.CacheExpiration;

    /// <summary>Gets the optional cache type.</summary>
    public CacheType? CacheType => _parameters.CacheType;

    /// <summary>Gets the command timeout in seconds.</summary>
    public int CommandTimeout => _parameters.CommandTimeout;

    /// <summary>
    ///     Exposes the underlying untyped parameters for existing CaeriusNet execution APIs.
    /// </summary>
    public StoredProcedureParameters AsUntyped()
    {
        return _parameters;
    }

    /// <summary>
    ///     Gets parameters as a read-only span for diagnostics and tests.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<SqlParameter> GetParametersSpan()
    {
        return _parameters.GetParametersSpan();
    }
}
