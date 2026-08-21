using System.Collections;
using System.Data.SqlTypes;
using CaeriusNet.Mappers;
using Microsoft.Data.SqlTypes;

namespace CaeriusNet.Commands;

public enum CaeriusCacheTier : byte
{
    Memory = 0,
    Frozen = 1,
    Redis = 2
}

public sealed record CaeriusCachePolicy(
    CaeriusCacheTier Tier,
    TimeSpan? Expiration = null,
    bool SlidingExpiration = false);

public sealed record StoredProcedureParameterDescriptor
{
    internal StoredProcedureParameterDescriptor(
        string name,
        object? value,
        SqlDbType dbType,
        ParameterDirection direction,
        int ordinal,
        int? size,
        byte? precision,
        byte? scale,
        string? typeName)
    {
        Name = name;
        Value = value;
        DbType = dbType;
        Direction = direction;
        Ordinal = ordinal;
        Size = size;
        Precision = precision;
        Scale = scale;
        TypeName = typeName;
    }

    public string Name { get; }
    public object? Value { get; }
    public SqlDbType DbType { get; }
    public ParameterDirection Direction { get; }
    public int Ordinal { get; }
    public int? Size { get; }
    public byte? Precision { get; }
    public byte? Scale { get; }
    public string? TypeName { get; }
}

public sealed class StoredProcedureCommand
{
    public const int UnspecifiedResultSetCapacity = -1;
    private string? _contractHash;

    internal StoredProcedureCommand(
        string schemaName,
        string procedureName,
        int commandTimeout,
        ImmutableArray<StoredProcedureParameterDescriptor> parameters,
        ImmutableArray<int> resultSetCapacities,
        string? contractHash,
        CaeriusCachePolicy? cachePolicy,
        ImmutableArray<string> dependencyTags,
        ImmutableArray<string> invalidationTags)
    {
        SchemaName = schemaName;
        ProcedureName = procedureName;
        FullName = string.Concat(schemaName, ".", procedureName);
        CommandTimeout = commandTimeout;
        Parameters = parameters;
        ResultSetCapacities = resultSetCapacities;
        _contractHash = contractHash;
        CachePolicy = cachePolicy;
        DependencyTags = dependencyTags;
        InvalidationTags = invalidationTags;
    }

    public string SchemaName { get; }
    public string ProcedureName { get; }
    public string FullName { get; }
    public int CommandTimeout { get; }
    public ImmutableArray<StoredProcedureParameterDescriptor> Parameters { get; }
    public ImmutableArray<int> ResultSetCapacities { get; }

    public string ContractHash => _contractHash ??= CaeriusCacheKeyBuilder.HashContract(
        SchemaName,
        ProcedureName,
        Parameters);

    public CaeriusCachePolicy? CachePolicy { get; }
    public ImmutableArray<string> DependencyTags { get; }
    public ImmutableArray<string> InvalidationTags { get; }

    internal SqlCommand CreateSqlCommand(
        SqlConnection connection,
        SqlTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var command = connection.CreateCommand();
        command.CommandText = FullName;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = CommandTimeout;
        command.Transaction = transaction;

        foreach (var descriptor in Parameters)
        {
            var parameter = new SqlParameter(descriptor.Name, descriptor.DbType)
            {
                Direction = descriptor.Direction
            };

            var value = descriptor.Value switch
            {
                null when descriptor.DbType == SqlDbType.Structured => null,
                null when descriptor.DbType == SqlDbType.Vector && descriptor.Size is { } vectorDimensions =>
                    SqlVector<float>.CreateNull(vectorDimensions),
                // SqlClient's Value property is a CLR-decimal projection.  It cannot carry a
                // decimal(29..38) output, whereas the SqlValue path preserves SqlDecimal's four
                // SQL data words.  Supplying SqlDecimal.Null gives the provider the same native
                // representation for output-only decimal parameters.
                null when descriptor.DbType == SqlDbType.Decimal => SqlDecimal.Null,
                null => DBNull.Value,
                ITvpParameterValue tvp => tvp.CreateValue(cancellationToken),
                var parameterValue => parameterValue
            };

            if (descriptor.DbType == SqlDbType.Decimal && value is SqlDecimal)
                parameter.SqlValue = value;
            else
                parameter.Value = value;

            if (descriptor.Size is { } size)
                parameter.Size = size;
            if (descriptor.Precision is { } precision)
                parameter.Precision = precision;
            if (descriptor.Scale is { } scale)
                parameter.Scale = scale;
            if (descriptor.TypeName is { Length: > 0 } typeName)
                parameter.TypeName = typeName;

            command.Parameters.Add(parameter);
        }

        return command;
    }
}

internal interface ITvpParameterValue
{
    bool IsCacheable { get; }
    object? CreateValue(CancellationToken cancellationToken);
    void AppendCacheKey(IncrementalHash hash);
}

internal sealed class ReplayableTvpParameterValue<T> : ITvpParameterValue
    where T : ITvpMapper<T>
{
    private readonly IReadOnlyCollection<T> _rows;

    internal ReplayableTvpParameterValue(IReadOnlyCollection<T> rows)
    {
        _rows = rows;
    }

    public bool IsCacheable => true;

    public object? CreateValue(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _rows.Count == 0 ? null : new TvpRecordEnumerable<T>(_rows);
    }

    public void AppendCacheKey(IncrementalHash hash)
    {
        ArgumentNullException.ThrowIfNull(hash);

        CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, "tvp:" + T.SqlTypeName);
        CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, T.Metadata.Length);
        foreach (var metadata in T.Metadata)
        {
            CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, metadata.Name);
            CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, (int)metadata.SqlDbType);
            CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, metadata.MaxLength);
            CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, metadata.Precision);
            CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, metadata.Scale);
            CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, metadata.LocaleId);
            CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, (int)metadata.CompareOptions);
        }

        CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, _rows.Count);
        var record = T.CreateRecord();
        foreach (var row in _rows)
        {
            T.WriteRow(record, row);
            for (var ordinal = 0; ordinal < record.FieldCount; ordinal++)
                CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, record.GetValue(ordinal));
        }
    }
}

internal sealed class StreamingTvpParameterValue<T>(Func<CancellationToken, IEnumerable<T>> rowsFactory)
    : ITvpParameterValue
    where T : ITvpMapper<T>
{
    public bool IsCacheable => false;

    public object? CreateValue(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = rowsFactory(cancellationToken) ??
                   throw new CaeriusContractException("The streaming TVP factory returned null.");
        var enumerator = rows.GetEnumerator();
        try
        {
            if (!enumerator.MoveNext())
            {
                enumerator.Dispose();
                return null;
            }

            return new TvpRecordEnumerable<T>(enumerator, enumerator.Current);
        }
        catch
        {
            enumerator.Dispose();
            throw;
        }
    }

    public void AppendCacheKey(IncrementalHash hash)
    {
        throw new CaeriusContractException("Streaming TVP parameters cannot participate in a cache key.");
    }
}

internal sealed class TvpRecordEnumerable<T> : IEnumerable<SqlDataRecord>
    where T : ITvpMapper<T>
{
    private readonly T _firstRow = default!;
    private readonly IEnumerator<T>? _prefetchedEnumerator;
    private readonly IEnumerable<T>? _rows;

    internal TvpRecordEnumerable(IEnumerable<T> rows)
    {
        _rows = rows;
    }

    internal TvpRecordEnumerable(IEnumerator<T> prefetchedEnumerator, T firstRow)
    {
        _prefetchedEnumerator = prefetchedEnumerator;
        _firstRow = firstRow;
    }

    public IEnumerator<SqlDataRecord> GetEnumerator()
    {
        return _prefetchedEnumerator is null
            ? Enumerate(_rows!)
            : EnumeratePrefetched(_prefetchedEnumerator, _firstRow);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private static IEnumerator<SqlDataRecord> Enumerate(IEnumerable<T> rows)
    {
        var record = T.CreateRecord();
        foreach (var row in rows)
        {
            T.WriteRow(record, row);
            yield return record;
        }
    }

    private static IEnumerator<SqlDataRecord> EnumeratePrefetched(IEnumerator<T> rows, T firstRow)
    {
        using (rows)
        {
            var record = T.CreateRecord();
            T.WriteRow(record, firstRow);
            yield return record;
            while (rows.MoveNext())
            {
                T.WriteRow(record, rows.Current);
                yield return record;
            }
        }
    }
}
