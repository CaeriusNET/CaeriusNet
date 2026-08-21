using System.Data;

namespace CaeriusNet.Analyzer.Roslyn;

/// <summary>
///     Validates the explicit SQL Server metadata declared by
///     <c>SqlServerTvpColumnAttribute</c> on a manually generated TVP record.
/// </summary>
/// <remarks>
///     This file is linked into both Roslyn components.  Keeping the extraction and validation here prevents
///     the analyzer from accepting a contract that the source generator would later reject (or vice versa).
/// </remarks>
internal static class ManualTvpColumnValidator
{
    private const string AttributeMetadataName = "CaeriusNet.SqlServerTvpColumnAttribute";

    internal static ManualTvpColumnValidation Validate(
        IMethodSymbol primaryConstructor,
        CancellationToken cancellationToken)
    {
        var columns = ImmutableArray.CreateBuilder<ManualTvpColumnContract>(primaryConstructor.Parameters.Length);
        var issues = ImmutableArray.CreateBuilder<ManualTvpColumnIssue>();

        for (var parameterIndex = 0; parameterIndex < primaryConstructor.Parameters.Length; parameterIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parameter = primaryConstructor.Parameters[parameterIndex];
            var hasFaithfulMapping = HasAnySupportedMapping(parameter.Type);
            if (!hasFaithfulMapping)
                issues.Add(new ManualTvpColumnIssue(
                    "CAERIUS005",
                    parameterIndex,
                    parameter.Name,
                    "its CLR type has no faithful SQL Server TVP mapping"));

            var attributes = parameter.GetAttributes()
                .Where(static attribute => string.Equals(
                    attribute.AttributeClass?.ToDisplayString(),
                    AttributeMetadataName,
                    StringComparison.Ordinal))
                .ToImmutableArray();

            if (attributes.Length != 1)
            {
                issues.Add(new ManualTvpColumnIssue(
                    "CAERIUS007",
                    parameterIndex,
                    parameter.Name,
                    "exactly one [SqlServerTvpColumn] attribute is required"));
                continue;
            }

            if (!TryReadContract(attributes[0], parameterIndex, parameter.Name, out var column, out var reason))
            {
                issues.Add(new ManualTvpColumnIssue("CAERIUS007", parameterIndex, parameter.Name, reason));
                continue;
            }

            ValidateColumn(parameter, column, hasFaithfulMapping, issues);
            columns.Add(column);
        }

        ValidateSet(columns, issues);

        return new ManualTvpColumnValidation(
            new EquatableArray<ManualTvpColumnContract>(columns.ToImmutable()),
            new EquatableArray<ManualTvpColumnIssue>(issues.ToImmutable()));
    }

    private static bool TryReadContract(
        AttributeData attribute,
        int parameterIndex,
        string parameterName,
        out ManualTvpColumnContract column,
        out string reason)
    {
        column = default;
        reason = "the attribute arguments could not be read";

        if (attribute.ConstructorArguments.Length != 4 ||
            attribute.ConstructorArguments[0].Value is not string name ||
            attribute.ConstructorArguments[1].Value is not int ordinal ||
            attribute.ConstructorArguments[2].Value is not int sqlDbType ||
            attribute.ConstructorArguments[3].Value is not bool isNullable)
            return false;

        column = new ManualTvpColumnContract(
            parameterIndex,
            parameterName,
            name,
            ordinal,
            sqlDbType,
            isNullable,
            GetOptionalInt64(attribute, "MaxLength"),
            GetOptionalByte(attribute, "Precision"),
            GetOptionalByte(attribute, "Scale"),
            GetOptionalInt64(attribute, "LocaleId"),
            GetOptionalInt64(attribute, "CompareOptions"));
        reason = string.Empty;
        return true;
    }

    private static long? GetOptionalInt64(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (!string.Equals(argument.Key, name, StringComparison.Ordinal))
                continue;

            var rawValue = argument.Value.Value switch
            {
                long longValue => longValue,
                int intValue => intValue,
                _ => (long?)null
            };

            return name switch
            {
                "MaxLength" when rawValue == long.MinValue => null,
                "LocaleId" or "CompareOptions" when rawValue == -1 => null,
                _ => rawValue
            };
        }

        return null;
    }

    private static byte? GetOptionalByte(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (!string.Equals(argument.Key, name, StringComparison.Ordinal))
                continue;

            return argument.Value.Value is byte value && value != byte.MaxValue ? value : null;
        }

        return null;
    }

    private static void ValidateColumn(
        IParameterSymbol parameter,
        ManualTvpColumnContract column,
        bool hasFaithfulMapping,
        ImmutableArray<ManualTvpColumnIssue>.Builder issues)
    {
        if (!IsValidSqlIdentifier(column.SqlName))
            AddInvalid(column, issues,
                "the SQL column name must be a regular ASCII identifier of 1 through 128 characters");

        if (column.Ordinal < 0)
            AddInvalid(column, issues, "the SQL ordinal must be zero or greater");

        if (!Enum.IsDefined(typeof(SqlDbType), column.SqlDbType))
        {
            AddInvalid(column, issues, "SqlDbType is not a defined SQL Server type");
            return;
        }

        var sqlDbType = (SqlDbType)column.SqlDbType;
        if (hasFaithfulMapping && !IsCompatible(parameter.Type, sqlDbType))
            AddInvalid(column, issues,
                $"SqlDbType.{sqlDbType} is not compatible with CLR type '{parameter.Type.ToDisplayString()}'");

        if (sqlDbType == SqlDbType.Decimal && column.Precision is { } precision)
        {
            var unwrapped = UnwrapNullableAndEnum(parameter.Type).ToDisplayString();
            if (precision > 28 && unwrapped != "System.Data.SqlTypes.SqlDecimal")
                AddInvalid(column, issues, "decimal precision 29 through 38 requires System.Data.SqlTypes.SqlDecimal");
            else if (precision <= 28 && unwrapped == "System.Data.SqlTypes.SqlDecimal")
                AddInvalid(column, issues,
                    "System.Data.SqlTypes.SqlDecimal is reserved for decimal precision 29 through 38");
        }

        if (column.IsNullable != IsNullable(parameter))
            AddInvalid(column, issues, "the attribute nullability must match the record parameter nullability");

        ValidateFacets(column, sqlDbType, issues);
    }

    private static void ValidateSet(
        ImmutableArray<ManualTvpColumnContract>.Builder columns,
        ImmutableArray<ManualTvpColumnIssue>.Builder issues)
    {
        var expectedOrdinal = 0;
        foreach (var column in columns.OrderBy(static column => column.Ordinal))
        {
            if (column.Ordinal != expectedOrdinal)
                AddInvalid(column, issues, "TVP ordinals must be zero-based, unique, and continuous");

            expectedOrdinal++;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
            if (!names.Add(column.SqlName))
                AddInvalid(column, issues, "SQL column names must be unique (case-insensitive)");
    }

    private static void ValidateFacets(
        ManualTvpColumnContract column,
        SqlDbType sqlDbType,
        ImmutableArray<ManualTvpColumnIssue>.Builder issues)
    {
        var sized = IsSized(sqlDbType);
        var collated = IsCollated(sqlDbType);
        var decimalType = sqlDbType == SqlDbType.Decimal;
        var scaledTemporal = sqlDbType is SqlDbType.Time or
            SqlDbType.DateTime2 or SqlDbType.DateTimeOffset;

        if (sized)
        {
            if (column.MaxLength is not { } maxLength)
                AddInvalid(column, issues, "MaxLength must be explicitly supplied for this SQL type");
            else if (!IsValidLength(sqlDbType, maxLength))
                AddInvalid(column, issues, "MaxLength is outside SQL Server's supported range for this SQL type");
        }
        else if (column.MaxLength is not null)
        {
            AddInvalid(column, issues, "MaxLength is only valid for character and binary SQL types");
        }

        if (decimalType)
        {
            if (column.Precision is not { } precision || column.Scale is not { } scale)
                AddInvalid(column, issues, "Precision and Scale must be explicitly supplied for decimal columns");
            else if (precision is < 1 or > 38 || scale > precision)
                AddInvalid(column, issues, "decimal Precision must be 1 through 38 and Scale cannot exceed Precision");
        }
        else if (scaledTemporal)
        {
            if (column.Scale is not { } scale)
                AddInvalid(column, issues,
                    "Scale must be explicitly supplied for time, datetime2, and datetimeoffset columns");
            else if (scale > 7)
                AddInvalid(column, issues, "Scale must be in the range 0 through 7 for this SQL type");

            if (column.Precision is not null)
                AddInvalid(column, issues, "Precision is not a SQL Server facet for this temporal SQL type");
        }
        else if (column.Precision is not null || column.Scale is not null)
        {
            AddInvalid(column, issues,
                "Precision and Scale are only valid for decimal and scale-bearing temporal SQL types");
        }

        if (collated)
        {
            if (column.LocaleId is not { } localeId || column.CompareOptions is not { } compareOptions)
                AddInvalid(column, issues,
                    "LocaleId and CompareOptions must be explicitly supplied for character columns");
            else if (localeId < 0 || compareOptions < 0)
                AddInvalid(column, issues, "LocaleId and CompareOptions must be non-negative");
        }
        else if (column.LocaleId is not null || column.CompareOptions is not null)
        {
            AddInvalid(column, issues, "LocaleId and CompareOptions are only valid for character SQL types");
        }
    }

    private static bool IsValidLength(SqlDbType sqlDbType, long value)
    {
        if (value == -1)
            return sqlDbType is SqlDbType.VarChar or
                SqlDbType.NVarChar or SqlDbType.VarBinary;

        if (value < 1)
            return false;

        return sqlDbType switch
        {
            SqlDbType.NChar or SqlDbType.NVarChar => value <= 4000,
            SqlDbType.Char or SqlDbType.VarChar or
                SqlDbType.Binary or SqlDbType.VarBinary => value <= 8000,
            _ => false
        };
    }

    private static bool IsSized(SqlDbType sqlDbType)
    {
        return sqlDbType is SqlDbType.Char or SqlDbType.NChar or
            SqlDbType.VarChar or SqlDbType.NVarChar or
            SqlDbType.Binary or SqlDbType.VarBinary;
    }

    private static bool IsCollated(SqlDbType sqlDbType)
    {
        return sqlDbType is SqlDbType.Char or SqlDbType.NChar or
            SqlDbType.VarChar or SqlDbType.NVarChar;
    }

    private static bool HasAnySupportedMapping(ITypeSymbol type)
    {
        type = UnwrapNullableAndEnum(type);
        return type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte or
                   SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
                   SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or
                   SpecialType.System_UInt64 or SpecialType.System_Decimal or SpecialType.System_Single
                   or SpecialType.System_Double or
                   SpecialType.System_String or SpecialType.System_Char or SpecialType.System_DateTime ||
               IsByteArray(type) ||
               type.ToDisplayString() is "System.Guid" or "System.DateTimeOffset" or "System.TimeSpan" or
                   "System.DateOnly" or "System.TimeOnly" or "System.Half" or "System.Data.SqlTypes.SqlDecimal";
    }

    private static bool IsCompatible(ITypeSymbol type, SqlDbType sqlDbType)
    {
        type = UnwrapNullableAndEnum(type);
        var display = type.ToDisplayString();

        return sqlDbType switch
        {
            SqlDbType.Bit => type.SpecialType == SpecialType.System_Boolean,
            SqlDbType.TinyInt => type.SpecialType == SpecialType.System_Byte,
            SqlDbType.SmallInt => type.SpecialType is SpecialType.System_SByte or SpecialType.System_Int16,
            SqlDbType.Int => type.SpecialType is SpecialType.System_UInt16 or SpecialType.System_Int32,
            SqlDbType.BigInt => type.SpecialType is SpecialType.System_UInt32 or SpecialType.System_Int64,
            SqlDbType.Decimal => type.SpecialType is SpecialType.System_UInt64 or SpecialType.System_Decimal ||
                                 display == "System.Data.SqlTypes.SqlDecimal",
            SqlDbType.Real => type.SpecialType == SpecialType.System_Single || display == "System.Half",
            SqlDbType.Float => type.SpecialType == SpecialType.System_Double,
            SqlDbType.NVarChar or SqlDbType.VarChar or
                SqlDbType.NChar or SqlDbType.Char =>
                type.SpecialType is SpecialType.System_String or SpecialType.System_Char,
            SqlDbType.Date or SqlDbType.Time =>
                sqlDbType == SqlDbType.Date
                    ? display == "System.DateOnly"
                    : display is "System.TimeSpan" or "System.TimeOnly",
            SqlDbType.DateTime or SqlDbType.DateTime2 or
                SqlDbType.SmallDateTime => type.SpecialType == SpecialType.System_DateTime,
            SqlDbType.DateTimeOffset => display == "System.DateTimeOffset",
            SqlDbType.UniqueIdentifier => display == "System.Guid",
            SqlDbType.Binary or SqlDbType.VarBinary => IsByteArray(type),
            _ => false
        };
    }

    private static ITypeSymbol UnwrapNullableAndEnum(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol
            {
                IsValueType: true,
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: > 0
            } nullable)
            type = nullable.TypeArguments[0];

        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: not null } enumType)
            type = enumType.EnumUnderlyingType;

        return type;
    }

    private static bool IsByteArray(ITypeSymbol type)
    {
        return type is IArrayTypeSymbol { Rank: 1, ElementType.SpecialType: SpecialType.System_Byte };
    }

    private static bool IsNullable(IParameterSymbol parameter)
    {
        return parameter.Type is INamedTypeSymbol
        {
            IsValueType: true,
            OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
        } || (parameter.Type.IsReferenceType && parameter.NullableAnnotation != NullableAnnotation.NotAnnotated);
    }

    private static bool IsValidSqlIdentifier(string identifier)
    {
        if (identifier.Length is < 1 or > 128 ||
            (!IsAsciiLetter(identifier[0]) && identifier[0] != '_'))
            return false;

        for (var index = 1; index < identifier.Length; index++)
        {
            var value = identifier[index];
            if (!IsAsciiLetter(value) && !char.IsDigit(value) && value != '_')
                return false;
        }

        return true;
    }

    private static bool IsAsciiLetter(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private static void AddInvalid(
        ManualTvpColumnContract column,
        ImmutableArray<ManualTvpColumnIssue>.Builder issues,
        string message)
    {
        issues.Add(new ManualTvpColumnIssue("CAERIUS007", column.ParameterIndex, column.ParameterName, message));
    }
}

internal readonly record struct ManualTvpColumnContract(
    int ParameterIndex,
    string ParameterName,
    string SqlName,
    int Ordinal,
    int SqlDbType,
    bool IsNullable,
    long? MaxLength,
    byte? Precision,
    byte? Scale,
    long? LocaleId,
    long? CompareOptions);

internal readonly record struct ManualTvpColumnIssue(
    string DiagnosticId,
    int ParameterIndex,
    string ParameterName,
    string Message);

internal readonly record struct ManualTvpColumnValidation(
    EquatableArray<ManualTvpColumnContract> Columns,
    EquatableArray<ManualTvpColumnIssue> Issues);
