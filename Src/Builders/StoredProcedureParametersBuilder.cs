using System.Collections;

namespace CaeriusNet.Builders;

/// <summary>
///     A builder class for configuring and creating stored procedure parameter collections.
/// </summary>
/// <remarks>
///     The <see cref="StoredProcedureParametersBuilder" /> class provides a fluent API for building
///     parameters for stored procedure execution. It supports regular SQL parameters, Table-Valued
///     Parameters (TVPs), and multiple caching mechanisms.
/// </remarks>
/// <param name="SchemaName">The schema name that owns the stored procedure (e.g., "dbo").</param>
/// <param name="ProcedureName">The name of the stored procedure to execute.</param>
/// <param name="ResultSetCapacity">
///     Expected number of rows in the result set (not parameters).
///     Used for pre-allocating collections to avoid resizing.
///     Default is 16.
/// </param>
/// <param name="CommandTimeout">
///     The wait time in seconds before terminating the command and raising an error. Defaults to 30.
/// </param>
public sealed record StoredProcedureParametersBuilder(
    string SchemaName,
    string ProcedureName,
    int ResultSetCapacity = 16,
    int CommandTimeout = 30)
{
    /// <summary>
    ///     SIMD-accelerated character sets for SQL identifier validation.
    /// </summary>
    private static readonly SearchValues<char> ValidIdentifierChars =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_");

    private static readonly SearchValues<char> ValidIdentifierStartChars =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_");

    private TimeSpan? _cacheExpiration;
    private string? _cacheKey;
    private CacheType? _cacheType;

    /// <summary>
    ///     Gets the collection of SQL parameters to be used in the stored procedure call.
    /// </summary>
    private List<SqlParameter> Parameters { get; } = new(4);

    /// <summary>
    ///     Adds a parameter to the stored procedure call.
    /// </summary>
    /// <param name="parameter">The name of the parameter.</param>
    /// <param name="value">The value of the parameter.</param>
    /// <param name="dbType">The SQL Server data type of the parameter.</param>
    /// <returns>The current builder instance to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="parameter" /> is null or empty.
    /// </exception>
    public StoredProcedureParametersBuilder AddParameter(string parameter, object? value, SqlDbType dbType)
    {
        return AddParameter(
            parameter,
            value,
            dbType,
            null);
    }

    /// <summary>
    ///     Adds a parameter to the stored procedure call with explicit SQL Server facets.
    /// </summary>
    /// <param name="parameter">The name of the parameter.</param>
    /// <param name="value">The value of the parameter.</param>
    /// <param name="dbType">The SQL Server data type of the parameter.</param>
    /// <param name="size">Optional parameter size for variable-length types.</param>
    /// <param name="precision">Optional decimal precision.</param>
    /// <param name="scale">Optional decimal scale.</param>
    /// <param name="direction">The parameter direction.</param>
    /// <returns>The current builder instance to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="parameter" /> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     <paramref name="parameter" /> is empty, whitespace, or uses an invalid SQL parameter prefix.
    /// </exception>
    public StoredProcedureParametersBuilder AddParameter(
        string parameter,
        object? value,
        SqlDbType dbType,
        int? size = null,
        byte? precision = null,
        byte? scale = null,
        ParameterDirection direction = ParameterDirection.Input)
    {
        if (direction is not (ParameterDirection.Input
            or ParameterDirection.Output
            or ParameterDirection.InputOutput
            or ParameterDirection.ReturnValue))
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported SQL parameter direction.");

        var sqlParameter = new SqlParameter(SqlParameterName.Normalize(parameter), dbType)
        {
            Value = value ?? DBNull.Value,
            Direction = direction
        };

        if (size is { } actualSize)
            sqlParameter.Size = actualSize;
        if (precision is { } actualPrecision)
            sqlParameter.Precision = actualPrecision;
        if (scale is { } actualScale)
            sqlParameter.Scale = actualScale;

        Parameters.Add(sqlParameter);
        return this;
    }

    internal StoredProcedureParametersBuilder AddParameter(SqlParameter parameter)
    {
        Parameters.Add(StoredProcedureParameters.CloneParameter(parameter));
        return this;
    }

    /// <summary>
    ///     Adds a Table-Valued Parameter (TVP) to the stored procedure call.
    /// </summary>
    /// <typeparam name="T">The type of objects in the TVP that implement <see cref="ITvpMapper{T}" />.</typeparam>
    /// <param name="parameter">The name of the TVP parameter.</param>
    /// <param name="items">The collection of items to include in the TVP.</param>
    /// <returns>The current builder instance to enable method chaining.</returns>
    /// <exception cref="ArgumentException">The items collection is empty.</exception>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="parameter" /> or <paramref name="items" /> is null.
    /// </exception>
    public StoredProcedureParametersBuilder AddTvpParameter<T>(string parameter, IEnumerable<T> items)
        where T : class, ITvpMapper<T>
    {
        ArgumentNullException.ThrowIfNull(items);
        var parameterName = SqlParameterName.Normalize(parameter);
        var (mapper, tvpItems) = GetTvpMapperAndItems(items);
        var typeName = T.TvpTypeName;
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        var currentTvpParameter = new SqlParameter(parameterName, SqlDbType.Structured)
        {
            TypeName = typeName,
            Value = new TvpParameterValue(() => mapper.MapAsSqlDataRecords(tvpItems))
        };

        Parameters.Add(currentTvpParameter);
        return this;
    }

    /// <summary>
    ///     Configures the stored procedure to use in-memory caching.
    /// </summary>
    /// <param name="cacheKey">The unique key for the cache entry.</param>
    /// <param name="expiration">The time span after which the cache entry expires.</param>
    /// <returns>The current builder instance to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="cacheKey" /> is null.
    /// </exception>
    public StoredProcedureParametersBuilder AddInMemoryCache(string cacheKey, TimeSpan expiration)
    {
        ValidateCacheKey(cacheKey);
        ValidatePositiveExpiration(expiration, nameof(expiration));

        _cacheKey = cacheKey;
        _cacheType = InMemory;
        _cacheExpiration = expiration;
        return this;
    }

    /// <summary>
    ///     Configures the stored procedure to use frozen (immutable) caching.
    /// </summary>
    /// <param name="cacheKey">The unique key for the frozen cache entry.</param>
    /// <returns>The current builder instance to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="cacheKey" /> is null.
    /// </exception>
    public StoredProcedureParametersBuilder AddFrozenCache(string cacheKey)
    {
        ValidateCacheKey(cacheKey);

        _cacheKey = cacheKey;
        _cacheType = Frozen;
        _cacheExpiration = null;
        return this;
    }

    /// <summary>
    ///     Configures the stored procedure to use Redis distributed caching.
    /// </summary>
    /// <param name="cacheKey">The unique key for the Redis cache entry.</param>
    /// <param name="expiration">Optional time span after which the Redis cache entry expires.</param>
    /// <returns>The current builder instance to enable method chaining.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="cacheKey" /> is null.
    /// </exception>
    public StoredProcedureParametersBuilder AddRedisCache(string cacheKey, TimeSpan? expiration = null)
    {
        ValidateCacheKey(cacheKey);
        ValidateOptionalPositiveExpiration(expiration, nameof(expiration));

        _cacheType = Redis;
        _cacheKey = cacheKey;
        _cacheExpiration = expiration;
        return this;
    }

    /// <summary>
    ///     Creates a new <see cref="StoredProcedureParameters" /> instance with the configured settings.
    /// </summary>
    /// <returns>A new <see cref="StoredProcedureParameters" /> object containing all specified parameters and settings.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if <see cref="SchemaName" /> or <see cref="ProcedureName" /> are null, empty, or contain invalid
    ///     characters. Valid SQL identifiers may only start with a letter or underscore, and contain letters,
    ///     digits, or underscores.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public StoredProcedureParameters Build()
    {
        if (!IsValidSqlIdentifier(SchemaName))
            throw new ArgumentException(
                $"'{SchemaName}' is not a valid SQL identifier. Use only letters, digits and underscores, starting with a letter or underscore.",
                nameof(SchemaName));

        if (!IsValidSqlIdentifier(ProcedureName))
            throw new ArgumentException(
                $"'{ProcedureName}' is not a valid SQL identifier. Use only letters, digits and underscores, starting with a letter or underscore.",
                nameof(ProcedureName));

        ArgumentOutOfRangeException.ThrowIfNegative(ResultSetCapacity);
        ArgumentOutOfRangeException.ThrowIfNegative(CommandTimeout);

        var parameters = Parameters.Count > 0
            ? Parameters.ToArray()
            : [];

        return new StoredProcedureParameters(
            SchemaName,
            ProcedureName,
            ResultSetCapacity,
            parameters,
            _cacheKey,
            _cacheExpiration,
            _cacheType,
            CommandTimeout);
    }

    /// <summary>
    ///     Validates that the given identifier is a safe SQL identifier using SIMD-accelerated search.
    /// </summary>
    private static bool IsValidSqlIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return false;
        var span = identifier.AsSpan();
        return ValidIdentifierStartChars.Contains(span[0])
               && !span[1..].ContainsAnyExcept(ValidIdentifierChars);
    }

    private static (T Mapper, IEnumerable<T> Items) GetTvpMapperAndItems<T>(IEnumerable<T> items)
        where T : class, ITvpMapper<T>
    {
        switch (items)
        {
            case IReadOnlyList<T> { Count: > 0 } readOnlyList:
                return (readOnlyList[0], readOnlyList);

            case IReadOnlyList<T>:
                break;

            case IList<T> { Count: > 0 } list:
                return (list[0], list);

            case IList<T>:
                break;

            default:
                var tvpItems = items as T[] ?? items.ToArray();
                if (tvpItems.Length > 0)
                    return (tvpItems[0], tvpItems);
                break;
        }

        throw new ArgumentException("No items found in the collection to map to a Table-Valued Parameter.",
            nameof(items));
    }

    private static void ValidateCacheKey(string cacheKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
    }

    private static void ValidateOptionalPositiveExpiration(TimeSpan? expiration, string paramName)
    {
        if (expiration is { } actualExpiration)
            ValidatePositiveExpiration(actualExpiration, paramName);
    }

    private static void ValidatePositiveExpiration(TimeSpan expiration, string paramName)
    {
        if (expiration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(paramName, expiration, "Cache expiration must be positive.");
    }

    private sealed class TvpParameterValue(Func<IEnumerable<SqlDataRecord>> factory) : IEnumerable<SqlDataRecord>
    {
        public IEnumerator<SqlDataRecord> GetEnumerator()
        {
            return factory().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
