using System.Collections;
using CaeriusNet.Mappers;

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
    internal StoredProcedureCommand(
        string schemaName,
        string procedureName,
        int commandTimeout,
        ImmutableArray<StoredProcedureParameterDescriptor> parameters,
        ImmutableArray<int> resultSetCapacities,
        string contractHash,
        CaeriusCachePolicy? cachePolicy,
        ImmutableArray<string> invalidationKeys)
    {
        SchemaName = schemaName;
        ProcedureName = procedureName;
        FullName = string.Concat(schemaName, ".", procedureName);
        CommandTimeout = commandTimeout;
        Parameters = parameters;
        ResultSetCapacities = resultSetCapacities;
        ContractHash = contractHash;
        CachePolicy = cachePolicy;
        InvalidationKeys = invalidationKeys;
    }

    public string SchemaName { get; }
    public string ProcedureName { get; }
    public string FullName { get; }
    public int CommandTimeout { get; }
    public ImmutableArray<StoredProcedureParameterDescriptor> Parameters { get; }
    public ImmutableArray<int> ResultSetCapacities { get; }
    public string ContractHash { get; }
    public CaeriusCachePolicy? CachePolicy { get; }
    public ImmutableArray<string> InvalidationKeys { get; }

    internal SqlCommand CreateSqlCommand(SqlConnection connection, SqlTransaction? transaction = null)
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
                Direction = descriptor.Direction,
                Value = descriptor.Value switch
                {
                    null when descriptor.DbType == SqlDbType.Structured => null,
                    null => DBNull.Value,
                    ITvpParameterValue tvp => tvp.CreateValue(),
                    var value => value
                }
            };

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
    object? CreateValue();
    void AppendCacheKey(IncrementalHash hash);
}

internal sealed class TvpParameterValue<T> : ITvpParameterValue
    where T : ITvpMapper<T>
{
    private readonly object _gate = new();
    private T[]? _materializedRows;
    private IEnumerable<T> _rows;

    internal TvpParameterValue(IEnumerable<T> rows)
    {
        _rows = rows;
        if (rows.TryGetNonEnumeratedCount(out var count) && count == 0)
            _materializedRows = [];
    }

    public object CreateValue()
    {
        lock (_gate)
        {
            if (_materializedRows is { } materialized)
                return materialized.Length == 0 ? null! : new TvpRecordEnumerable<T>(materialized);

            // Microsoft.Data.SqlClient requires a null value for an empty TVP; it rejects an empty
            // IEnumerable<SqlDataRecord> because there is no record from which to read provider metadata.
            // Counted collections take the zero-allocation path. For an unknown, potentially very large
            // stream, peek just one source row and replay it through a record enumerable: no full buffering,
            // no dependency on a "first row" for the static TVP metadata, and empty streams still become null.
            if (_rows.TryGetNonEnumeratedCount(out var count))
                return count == 0 ? null! : new TvpRecordEnumerable<T>(_rows);

            var enumerator = _rows.GetEnumerator();
            try
            {
                if (!enumerator.MoveNext())
                {
                    enumerator.Dispose();
                    return null!;
                }

                return new TvpRecordEnumerable<T>(enumerator, enumerator.Current);
            }
            catch
            {
                enumerator.Dispose();
                throw;
            }
        }
    }

    public void AppendCacheKey(IncrementalHash hash)
    {
        ArgumentNullException.ThrowIfNull(hash);

        T[] rows;
        lock (_gate)
        {
            rows = _materializedRows ??= _rows.ToArray();
            _rows = rows;
        }

        CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, "tvp:" + T.SqlTypeName);
        CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, T.Metadata.Length);
        foreach (var metadata in T.Metadata)
        {
            CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, metadata.Name);
            CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, (int)metadata.SqlDbType);
            CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, metadata.MaxLength);
            CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, metadata.Precision);
            CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, metadata.Scale);
        }

        CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, rows.Length);
        var record = T.CreateRecord();
        foreach (var row in rows)
        {
            T.WriteRow(record, row);
            for (var ordinal = 0; ordinal < record.FieldCount; ordinal++)
                CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, record.GetValue(ordinal));
        }
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
