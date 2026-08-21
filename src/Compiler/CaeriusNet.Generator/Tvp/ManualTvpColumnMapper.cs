using System.Data;

namespace CaeriusNet.Generator.Tvp;

/// <summary>Converts a validated manual TVP attribute contract into generated SqlMetaData source.</summary>
internal static class ManualTvpColumnMapper
{
    internal static ColumnModel? Create(IParameterSymbol parameter, ManualTvpColumnContract contract)
    {
        var inferred = ColumnExtractor.Extract(parameter, contract.Ordinal);
        if (inferred is null || !Enum.IsDefined(typeof(SqlDbType), contract.SqlDbType))
            return null;

        var sqlDbType = (SqlDbType)contract.SqlDbType;
        var metadataConstructor = BuildSqlMetaDataConstructor(contract, sqlDbType);
        if (metadataConstructor is null)
            return null;

        return inferred with
        {
            OrdinalPosition = contract.Ordinal,
            SqlType = sqlDbType.ToString(),
            SqlMetaDataConstructor = metadataConstructor
        };
    }

    private static string? BuildSqlMetaDataConstructor(
        ManualTvpColumnContract column,
        SqlDbType sqlDbType)
    {
        var name = SymbolDisplay.FormatLiteral(column.SqlName, true);
        var type = "SqlDbType." + sqlDbType;

        if (IsCollated(sqlDbType))
        {
            if (column.MaxLength is not { } length || column.LocaleId is not { } localeId ||
                column.CompareOptions is not { } compareOptions)
                return null;

            return $"new SqlMetaData({name}, {type}, {length.ToString(CultureInfo.InvariantCulture)}, " +
                   $"{localeId.ToString(CultureInfo.InvariantCulture)}, " +
                   $"(global::System.Data.SqlTypes.SqlCompareOptions){compareOptions.ToString(CultureInfo.InvariantCulture)})";
        }

        if (IsSized(sqlDbType))
        {
            if (column.MaxLength is not { } length)
                return null;

            return $"new SqlMetaData({name}, {type}, {length.ToString(CultureInfo.InvariantCulture)})";
        }

        if (sqlDbType == SqlDbType.Decimal)
        {
            if (column.Precision is not { } precision || column.Scale is not { } scale)
                return null;

            return $"new SqlMetaData({name}, {type}, {precision.ToString(CultureInfo.InvariantCulture)}, " +
                   $"{scale.ToString(CultureInfo.InvariantCulture)})";
        }

        if (sqlDbType is SqlDbType.Time or SqlDbType.DateTime2 or
            SqlDbType.DateTimeOffset)
        {
            if (column.Scale is not { } scale)
                return null;

            return $"new SqlMetaData({name}, {type}, 0, {scale.ToString(CultureInfo.InvariantCulture)})";
        }

        return $"new SqlMetaData({name}, {type})";
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
}
