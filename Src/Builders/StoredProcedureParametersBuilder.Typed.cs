using System.Collections;

namespace CaeriusNet.Builders;

/// <summary>
///     Maps a generated TVP row into a reusable <see cref="SqlDataRecord" />.
/// </summary>
/// <typeparam name="T">The generated TVP row type.</typeparam>
/// <param name="record">The SQL data record to populate.</param>
/// <param name="row">The generated TVP row value.</param>
public delegate void GeneratedTvpRowMapper<in T>(SqlDataRecord record, T row);

/// <summary>
///     A typed stored procedure parameter builder for generated SQL Server contracts.
/// </summary>
/// <typeparam name="TProcedure">The generated procedure descriptor type.</typeparam>
public sealed class StoredProcedureParametersBuilder<TProcedure>
    where TProcedure : struct, ICaeriusGeneratedProcedure<TProcedure>
{
    private readonly StoredProcedureParametersBuilder _inner;
    private int _generatedParameterCount;
    private bool _generatedParametersBound;

    private StoredProcedureParametersBuilder(int resultSetCapacity, int commandTimeout)
    {
        _inner = new StoredProcedureParametersBuilder(
            TProcedure.SchemaName,
            TProcedure.ProcedureName,
            resultSetCapacity,
            commandTimeout);

        _generatedParametersBound = TProcedure.ParameterCount == 0;
    }

    /// <summary>
    ///     Creates a typed builder for <typeparamref name="TProcedure" />.
    /// </summary>
    /// <param name="resultSetCapacity">Expected result-set capacity used for collection preallocation.</param>
    /// <param name="commandTimeout">Command timeout in seconds.</param>
    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification =
            "The generated API intentionally follows StoredProcedureParametersBuilder<TProcedure>.Create(...).")]
    public static StoredProcedureParametersBuilder<TProcedure> Create(
        int resultSetCapacity,
        int commandTimeout = 30)
    {
        return new StoredProcedureParametersBuilder<TProcedure>(resultSetCapacity, commandTimeout);
    }

    /// <summary>
    ///     Creates a typed builder and binds a generated parameter record in one step.
    /// </summary>
    /// <typeparam name="TParameters">Generated parameter record type for <typeparamref name="TProcedure" />.</typeparam>
    /// <param name="parameters">Generated parameter record.</param>
    /// <param name="resultSetCapacity">Expected result-set capacity used for collection preallocation.</param>
    /// <param name="commandTimeout">Command timeout in seconds.</param>
    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification =
            "The generated API intentionally follows StoredProcedureParametersBuilder<TProcedure>.Create(...).")]
    public static StoredProcedureParametersBuilder<TProcedure> Create<TParameters>(
        TParameters parameters,
        int resultSetCapacity,
        int commandTimeout = 30)
        where TParameters : ICaeriusGeneratedProcedureParameters<TProcedure, TParameters>
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var builder = Create(resultSetCapacity, commandTimeout);
        TParameters.Bind(builder, parameters);
        return builder;
    }

    /// <summary>
    ///     Adds a generated scalar parameter in manifest ordinal order.
    /// </summary>
    public StoredProcedureParametersBuilder<TProcedure> AddGeneratedParameter(
        int ordinal,
        string name,
        object? value,
        SqlDbType dbType,
        int? size = null,
        byte? precision = null,
        byte? scale = null)
    {
        var parameter = new SqlParameter(SqlParameterName.Normalize(name), dbType) { Value = value ?? DBNull.Value };
        if (size is { } actualSize)
            parameter.Size = actualSize;
        if (precision is { } actualPrecision)
            parameter.Precision = actualPrecision;
        if (scale is { } actualScale)
            parameter.Scale = actualScale;

        return AddGeneratedSqlParameter(ordinal, parameter);
    }

    /// <summary>
    ///     Adds a generated SQL parameter in manifest ordinal order.
    /// </summary>
    public StoredProcedureParametersBuilder<TProcedure> AddGeneratedParameter(
        int ordinal,
        SqlParameter parameter)
    {
        return AddGeneratedSqlParameter(ordinal, parameter);
    }

    /// <summary>
    ///     Adds a generated TVP parameter backed by <see cref="ReadOnlyMemory{T}" /> rows.
    /// </summary>
    public StoredProcedureParametersBuilder<TProcedure> AddGeneratedTvpParameter<T>(
        int ordinal,
        string name,
        string typeName,
        SqlMetaData[] metadata,
        ReadOnlyMemory<T> rows,
        GeneratedTvpRowMapper<T> mapRow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(mapRow);
        var parameterName = SqlParameterName.Normalize(name);
        if (metadata.Length == 0)
            throw new ArgumentException(
                $"Generated TVP parameter '{name}' for procedure '{TProcedure.FullName}' must declare at least one column.",
                nameof(metadata));

        var parameter = new SqlParameter(parameterName, SqlDbType.Structured)
        {
            TypeName = typeName,
            Value = new ReadOnlyMemoryTvpParameterValue<T>(metadata, rows, mapRow)
        };

        return AddGeneratedSqlParameter(ordinal, parameter);
    }

    /// <summary>
    ///     Marks the generated parameter set as complete.
    /// </summary>
    public StoredProcedureParametersBuilder<TProcedure> MarkGeneratedParametersBound()
    {
        if (_generatedParameterCount != TProcedure.ParameterCount)
            throw new InvalidOperationException(
                $"Generated procedure '{TProcedure.FullName}' expects {TProcedure.ParameterCount} generated parameter(s) but {_generatedParameterCount} were bound.");

        _generatedParametersBound = true;
        return this;
    }

    /// <summary>
    ///     Configures in-memory caching.
    /// </summary>
    public StoredProcedureParametersBuilder<TProcedure> AddInMemoryCache(string cacheKey, TimeSpan expiration)
    {
        _inner.AddInMemoryCache(cacheKey, expiration);
        return this;
    }

    /// <summary>
    ///     Configures frozen caching.
    /// </summary>
    public StoredProcedureParametersBuilder<TProcedure> AddFrozenCache(string cacheKey)
    {
        _inner.AddFrozenCache(cacheKey);
        return this;
    }

    /// <summary>
    ///     Configures Redis caching.
    /// </summary>
    public StoredProcedureParametersBuilder<TProcedure> AddRedisCache(string cacheKey, TimeSpan? expiration = null)
    {
        _inner.AddRedisCache(cacheKey, expiration);
        return this;
    }

    /// <summary>
    ///     Builds typed stored procedure parameters for the generated procedure descriptor.
    /// </summary>
    public StoredProcedureParameters<TProcedure> Build()
    {
        if (!_generatedParametersBound)
            throw new InvalidOperationException(
                $"Generated parameters for procedure '{TProcedure.FullName}' were not bound. Call the generated WithParameters extension before Build.");

        return new StoredProcedureParameters<TProcedure>(_inner.Build());
    }

    private void ValidateGeneratedOrdinal(int ordinal)
    {
        if (ordinal < 1)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                ordinal,
                $"Generated parameter ordinals for procedure '{TProcedure.FullName}' are 1-based.");

        if (ordinal > TProcedure.ParameterCount)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                ordinal,
                $"Generated procedure '{TProcedure.FullName}' expects only {TProcedure.ParameterCount} generated parameter(s).");

        var expectedOrdinal = _generatedParameterCount + 1;
        if (ordinal != expectedOrdinal)
            throw new ArgumentOutOfRangeException(
                nameof(ordinal),
                ordinal,
                $"Generated parameters for procedure '{TProcedure.FullName}' must be added in manifest order. Expected ordinal {expectedOrdinal}.");
    }

    private StoredProcedureParametersBuilder<TProcedure> AddGeneratedSqlParameter(int ordinal, SqlParameter parameter)
    {
        ValidateGeneratedOrdinal(ordinal);
        ArgumentNullException.ThrowIfNull(parameter);

        _inner.AddParameter(parameter);
        _generatedParameterCount++;
        _generatedParametersBound = false;
        return this;
    }

    private sealed class ReadOnlyMemoryTvpParameterValue<T>(
        SqlMetaData[] metadata,
        ReadOnlyMemory<T> rows,
        GeneratedTvpRowMapper<T> mapRow) : IEnumerable<SqlDataRecord>
    {
        public IEnumerator<SqlDataRecord> GetEnumerator()
        {
            return new Enumerator(metadata, rows, mapRow);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private sealed class Enumerator(
            SqlMetaData[] metadata,
            ReadOnlyMemory<T> rows,
            GeneratedTvpRowMapper<T> mapRow) : IEnumerator<SqlDataRecord>
        {
            private int _index = -1;

            public SqlDataRecord Current { get; } = new(metadata);

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                var next = _index + 1;
                if (next >= rows.Length)
                    return false;

                _index = next;
                mapRow(Current, rows.Span[next]);
                return true;
            }

            public void Reset()
            {
                _index = -1;
            }

            public void Dispose()
            {
            }
        }
    }
}
