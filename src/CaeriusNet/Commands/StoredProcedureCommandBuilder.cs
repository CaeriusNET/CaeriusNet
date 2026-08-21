using System.Data.SqlTypes;
using CaeriusNet.Mappers;

namespace CaeriusNet.Commands;

public sealed class StoredProcedureCommandBuilder
{
    private readonly HashSet<string> _dependencyTags = new(StringComparer.Ordinal);
    private readonly HashSet<string> _invalidationTags = new(StringComparer.Ordinal);
    private readonly List<StoredProcedureParameterDescriptor> _parameters = new(4);
    private readonly List<int> _resultSetCapacities = [StoredProcedureCommand.UnspecifiedResultSetCapacity];
    private CaeriusCachePolicy? _cachePolicy;
    private string? _contractHash;

    public StoredProcedureCommandBuilder(
        string schemaName,
        string procedureName,
        int commandTimeout = 30)
    {
        SchemaName = schemaName;
        ProcedureName = procedureName;
        CommandTimeout = commandTimeout;
    }

    public string SchemaName { get; }
    public string ProcedureName { get; }
    public int CommandTimeout { get; }

    public StoredProcedureCommandBuilder AddParameter(
        string name,
        object? value,
        SqlDbType dbType,
        ParameterDirection direction = ParameterDirection.Input,
        int? size = null,
        byte? precision = null,
        byte? scale = null,
        int? ordinal = null)
    {
        if (dbType == SqlDbType.Structured)
            throw new ArgumentException("Use AddTvpParameter for structured parameters.", nameof(dbType));

        var normalizedName = NormalizeParameterName(name);
        var actualOrdinal = ordinal ?? _parameters.Count;
        ValidateDescriptor(normalizedName, value, dbType, direction, actualOrdinal, size, precision, scale, null);

        _parameters.Add(new StoredProcedureParameterDescriptor(
            normalizedName, value, dbType, direction, actualOrdinal, size, precision, scale, null));
        return this;
    }

    public StoredProcedureCommandBuilder AddOutputParameter(
        string name,
        SqlDbType dbType,
        int? size = null,
        byte? precision = null,
        byte? scale = null,
        int? ordinal = null)
    {
        return AddParameter(name, null, dbType, ParameterDirection.Output, size, precision, scale, ordinal);
    }

    public StoredProcedureCommandBuilder AddInputOutputParameter(
        string name,
        object? value,
        SqlDbType dbType,
        int? size = null,
        byte? precision = null,
        byte? scale = null,
        int? ordinal = null)
    {
        return AddParameter(name, value, dbType, ParameterDirection.InputOutput, size, precision, scale, ordinal);
    }

    public StoredProcedureCommandBuilder AddReturnValue(
        string name = "@returnValue",
        SqlDbType dbType = SqlDbType.Int,
        int? ordinal = null)
    {
        return AddParameter(name, null, dbType, ParameterDirection.ReturnValue, ordinal: ordinal);
    }

    public StoredProcedureCommandBuilder AddTvpParameter<T>(
        string name,
        IReadOnlyCollection<T> rows,
        int? ordinal = null)
        where T : ITvpMapper<T>
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (string.IsNullOrWhiteSpace(T.SqlTypeName))
            throw new CaeriusContractException($"{typeof(T).FullName} declares an empty SQL TVP type name.");
        if (T.Metadata.Length == 0)
            throw new CaeriusContractException($"{typeof(T).FullName} declares no TVP columns.");

        var normalizedName = NormalizeParameterName(name);
        var actualOrdinal = ordinal ?? _parameters.Count;
        ValidateDescriptor(
            normalizedName,
            rows,
            SqlDbType.Structured,
            ParameterDirection.Input,
            actualOrdinal,
            null,
            null,
            null,
            T.SqlTypeName);

        _parameters.Add(new StoredProcedureParameterDescriptor(
            normalizedName,
            new ReplayableTvpParameterValue<T>(rows),
            SqlDbType.Structured,
            ParameterDirection.Input,
            actualOrdinal,
            null,
            null,
            null,
            T.SqlTypeName));
        return this;
    }

    public StoredProcedureCommandBuilder AddTvpParameter<T>(
        string name,
        Func<CancellationToken, IEnumerable<T>> rowsFactory,
        int? ordinal = null)
        where T : ITvpMapper<T>
    {
        ArgumentNullException.ThrowIfNull(rowsFactory);
        ValidateTvpMapper<T>();
        var normalizedName = NormalizeParameterName(name);
        var actualOrdinal = ordinal ?? _parameters.Count;
        var value = new StreamingTvpParameterValue<T>(rowsFactory);
        ValidateDescriptor(normalizedName, value, SqlDbType.Structured, ParameterDirection.Input, actualOrdinal,
            null, null, null, T.SqlTypeName);
        _parameters.Add(new StoredProcedureParameterDescriptor(
            normalizedName, value, SqlDbType.Structured, ParameterDirection.Input, actualOrdinal,
            null, null, null, T.SqlTypeName));
        return this;
    }

    public StoredProcedureCommandBuilder WithResultSetCapacities(params int[] capacities)
    {
        ArgumentNullException.ThrowIfNull(capacities);
        if (capacities is not { Length: >= 1 and <= 10 })
            throw new ArgumentOutOfRangeException(nameof(capacities), "Between one and ten capacities are required.");
        if (capacities.Any(static capacity => capacity < StoredProcedureCommand.UnspecifiedResultSetCapacity))
            throw new ArgumentOutOfRangeException(nameof(capacities),
                "Result-set capacities must be -1 or non-negative.");
        _resultSetCapacities.Clear();
        _resultSetCapacities.AddRange(capacities);
        return this;
    }

    public StoredProcedureCommandBuilder WithContractHash(string contractHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractHash);
        _contractHash = contractHash;
        return this;
    }

    public StoredProcedureCommandBuilder UseInMemoryCache(TimeSpan expiration, bool slidingExpiration = false)
    {
        ValidateExpiration(expiration);
        _cachePolicy = new CaeriusCachePolicy(CaeriusCacheTier.Memory, expiration, slidingExpiration);
        return this;
    }

    public StoredProcedureCommandBuilder UseFrozenCache()
    {
        _cachePolicy = new CaeriusCachePolicy(CaeriusCacheTier.Frozen);
        return this;
    }

    public StoredProcedureCommandBuilder UseRedisCache(TimeSpan? expiration = null)
    {
        if (expiration is { } value)
            ValidateExpiration(value);
        _cachePolicy = new CaeriusCachePolicy(CaeriusCacheTier.Redis, expiration);
        return this;
    }

    public StoredProcedureCommandBuilder DependsOn(params string[] tags)
    {
        AddTags(tags, _dependencyTags);
        return this;
    }

    public StoredProcedureCommandBuilder Invalidates(params string[] tags)
    {
        AddTags(tags, _invalidationTags);
        return this;
    }

    public StoredProcedureCommand Build()
    {
        ValidateIdentifier(SchemaName, nameof(SchemaName));
        ValidateIdentifier(ProcedureName, nameof(ProcedureName));
        if (CommandTimeout <= 0)
            throw new ArgumentOutOfRangeException(nameof(CommandTimeout), "Command timeout must be positive.");

        var duplicate = _parameters
            .GroupBy(static parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
            throw new CaeriusContractException($"Duplicate SQL parameter '{duplicate.Key}'.");

        var returnValues = _parameters.Count(static parameter => parameter.Direction == ParameterDirection.ReturnValue);
        if (returnValues > 1)
            throw new CaeriusContractException("A stored procedure command can declare only one return value.");

        var ordered = _parameters.OrderBy(static parameter => parameter.Ordinal).ToImmutableArray();
        for (var index = 0; index < ordered.Length; index++)
            if (ordered[index].Ordinal != index)
                throw new CaeriusContractException(
                    $"Parameter ordinals must be continuous from zero; expected {index}, found {ordered[index].Ordinal}.");

        if (_cachePolicy is not null && ordered.Any(static parameter =>
                parameter.Value is ITvpParameterValue { IsCacheable: false }))
            throw new CaeriusContractException("Commands with a streaming TVP parameter cannot be cached.");

        return new StoredProcedureCommand(
            SchemaName,
            ProcedureName,
            CommandTimeout,
            ordered,
            _resultSetCapacities.ToImmutableArray(),
            _contractHash,
            _cachePolicy,
            _dependencyTags.Order(StringComparer.Ordinal).ToImmutableArray(),
            _invalidationTags.Order(StringComparer.Ordinal).ToImmutableArray());
    }

    public static string NormalizeParameterName(string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        if (!string.Equals(parameterName, parameterName.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("SQL parameter names cannot have surrounding whitespace.",
                nameof(parameterName));
        var name = parameterName[0] == '@' ? parameterName : "@" + parameterName;
        if (name.Length == 1 || (name.Length > 1 && name[1] == '@'))
            throw new ArgumentException("SQL parameter names require one leading '@' and a name.",
                nameof(parameterName));
        ValidateIdentifier(name.AsSpan(1), nameof(parameterName));
        return name;
    }

    private static void ValidateDescriptor(
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
        if (!Enum.IsDefined(dbType))
            throw new ArgumentOutOfRangeException(nameof(dbType));
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        if (size is 0 or < -1)
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive or -1 for MAX.");
        if (precision is 0 or > 38)
            throw new ArgumentOutOfRangeException(nameof(precision), "SQL decimal precision must be between 1 and 38.");
        if (scale is > 38 || (precision is { } p && scale is { } s && s > p))
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale cannot exceed precision or 38.");
        if (dbType == SqlDbType.Structured &&
            (direction != ParameterDirection.Input || string.IsNullOrWhiteSpace(typeName)))
            throw new CaeriusContractException(
                $"Structured parameter '{name}' must be input-only and declare a SQL type name.");
        if (direction == ParameterDirection.ReturnValue && dbType == SqlDbType.Structured)
            throw new CaeriusContractException("A return value cannot be structured.");
        if (direction == ParameterDirection.ReturnValue && (dbType != SqlDbType.Int || ordinal != 0))
            throw new CaeriusContractException("A return value must be SqlDbType.Int at ordinal 0.");
        if (direction is ParameterDirection.Output or ParameterDirection.ReturnValue && !IsNullValue(value))
            throw new CaeriusContractException($"Output parameter '{name}' cannot declare an input value.");
        if (size is not null && !IsSizedType(dbType))
            throw new CaeriusContractException($"Size is not valid for SQL type {dbType} on parameter '{name}'.");
        if (precision is not null && dbType != SqlDbType.Decimal)
            throw new CaeriusContractException($"Precision is only valid for Decimal parameters ('{name}').");
        if (scale is not null && dbType != SqlDbType.Decimal && !IsScaleSensitiveDateTimeType(dbType))
            throw new CaeriusContractException($"Scale is not valid for SQL type {dbType} on parameter '{name}'.");

        var requiresStableFacets =
            IsNullValue(value) || direction is ParameterDirection.Output or ParameterDirection.InputOutput;
        if (requiresStableFacets && IsSizedType(dbType) && size is null)
            throw new CaeriusContractException(
                $"Parameter '{name}' requires Size because it is {DescribeFacetReason(value, direction)} and has SQL type {dbType}.");
        if (requiresStableFacets && dbType == SqlDbType.Decimal && (precision is null || scale is null))
            throw new CaeriusContractException(
                $"Parameter '{name}' requires both Precision and Scale because it is {DescribeFacetReason(value, direction)} and has SQL type Decimal.");
        if (requiresStableFacets && IsScaleSensitiveDateTimeType(dbType) && scale is null)
            throw new CaeriusContractException(
                $"Parameter '{name}' requires Scale because it is {DescribeFacetReason(value, direction)} and has SQL type {dbType}.");
    }

    private static bool IsSizedType(SqlDbType dbType)
    {
        return dbType is SqlDbType.Binary or SqlDbType.VarBinary or
            SqlDbType.Char or SqlDbType.NChar or SqlDbType.VarChar or SqlDbType.NVarChar or
            SqlDbType.Image or SqlDbType.Text or SqlDbType.NText or SqlDbType.Vector;
    }

    private static bool IsScaleSensitiveDateTimeType(SqlDbType dbType)
    {
        return dbType is SqlDbType.Time or SqlDbType.DateTime2 or SqlDbType.DateTimeOffset;
    }

    private static bool IsNullValue(object? value)
    {
        return value is null or DBNull || value is INullable { IsNull: true };
    }

    private static string DescribeFacetReason(object? value, ParameterDirection direction)
    {
        return direction switch
        {
            ParameterDirection.Output => "an output parameter",
            ParameterDirection.InputOutput => "an input/output parameter",
            _ when IsNullValue(value) => "a null-valued parameter",
            _ => "a parameter requiring explicit facets"
        };
    }

    private static void ValidateExpiration(TimeSpan expiration)
    {
        if (expiration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expiration), "Cache expiration must be positive.");
    }

    internal static void ValidateIdentifier(string value, string parameterName)
    {
        ValidateIdentifier(value.AsSpan(), parameterName);
    }

    private static void ValidateIdentifier(ReadOnlySpan<char> value, string parameterName)
    {
        if (value.IsEmpty || value.Length > 128 || value.IsWhiteSpace() ||
            !(char.IsLetter(value[0]) || value[0] == '_'))
            throw new ArgumentException("The value is not a valid SQL identifier.", parameterName);
        for (var index = 1; index < value.Length; index++)
            if (!(char.IsLetterOrDigit(value[index]) || value[index] == '_'))
                throw new ArgumentException("The value is not a valid SQL identifier.", parameterName);
    }

    private static void ValidateTvpMapper<T>() where T : ITvpMapper<T>
    {
        if (string.IsNullOrWhiteSpace(T.SqlTypeName))
            throw new CaeriusContractException($"{typeof(T).FullName} declares an empty SQL TVP type name.");
        if (T.Metadata.Length == 0)
            throw new CaeriusContractException($"{typeof(T).FullName} declares no TVP columns.");
    }

    private static void AddTags(string[] tags, HashSet<string> destination)
    {
        ArgumentNullException.ThrowIfNull(tags);
        foreach (var tag in tags)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tag);
            if (tag.Length > 256)
                throw new ArgumentOutOfRangeException(nameof(tags), "Cache tags cannot exceed 256 characters.");
            destination.Add(tag);
        }
    }
}

public sealed class StoredProcedureCommandBuilder<TProcedure>
    where TProcedure : struct, ICaeriusGeneratedProcedure<TProcedure>
{
    private readonly StoredProcedureCommandBuilder _builder;

    private StoredProcedureCommandBuilder(int commandTimeout)
    {
        _builder = new StoredProcedureCommandBuilder(TProcedure.SchemaName, TProcedure.ProcedureName, commandTimeout)
            .WithContractHash(TProcedure.ContractHash);
    }

    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "The static factory is the typed entry point for the static generated-procedure contract.")]
    public static StoredProcedureCommandBuilder<TProcedure> Create(int commandTimeout = 30)
    {
        return new StoredProcedureCommandBuilder<TProcedure>(commandTimeout);
    }

    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "The static factory binds a generated parameter record through its required static contract.")]
    public static StoredProcedureCommandBuilder<TProcedure> Create<TParameters>(
        TParameters parameters,
        int commandTimeout = 30)
        where TParameters : ICaeriusGeneratedProcedureParameters<TProcedure, TParameters>
    {
        var builder = new StoredProcedureCommandBuilder<TProcedure>(commandTimeout);
        TParameters.Bind(builder, parameters);
        return builder;
    }

    public StoredProcedureCommandBuilder<TProcedure> AddParameter(
        string name,
        object? value,
        SqlDbType dbType,
        ParameterDirection direction = ParameterDirection.Input,
        int? size = null,
        byte? precision = null,
        byte? scale = null,
        int? ordinal = null)
    {
        _builder.AddParameter(name, value, dbType, direction, size, precision, scale, ordinal);
        return this;
    }

    public StoredProcedureCommandBuilder<TProcedure> AddTvpParameter<T>(string name, IReadOnlyCollection<T> rows,
        int? ordinal = null)
        where T : ITvpMapper<T>
    {
        _builder.AddTvpParameter(name, rows, ordinal);
        return this;
    }

    public StoredProcedureCommandBuilder<TProcedure> AddTvpParameter<T>(
        string name,
        Func<CancellationToken, IEnumerable<T>> rowsFactory,
        int? ordinal = null)
        where T : ITvpMapper<T>
    {
        _builder.AddTvpParameter(name, rowsFactory, ordinal);
        return this;
    }

    public StoredProcedureCommandBuilder<TProcedure> WithResultSetCapacities(params int[] capacities)
    {
        _builder.WithResultSetCapacities(capacities);
        return this;
    }

    public StoredProcedureCommandBuilder<TProcedure> UseInMemoryCache(TimeSpan expiration, bool sliding = false)
    {
        _builder.UseInMemoryCache(expiration, sliding);
        return this;
    }

    public StoredProcedureCommandBuilder<TProcedure> UseFrozenCache()
    {
        _builder.UseFrozenCache();
        return this;
    }

    public StoredProcedureCommandBuilder<TProcedure> UseRedisCache(TimeSpan? expiration = null)
    {
        _builder.UseRedisCache(expiration);
        return this;
    }

    public StoredProcedureCommandBuilder<TProcedure> DependsOn(params string[] tags)
    {
        _builder.DependsOn(tags);
        return this;
    }

    public StoredProcedureCommandBuilder<TProcedure> Invalidates(params string[] tags)
    {
        _builder.Invalidates(tags);
        return this;
    }

    public StoredProcedureCommand Build()
    {
        return _builder.Build();
    }
}
