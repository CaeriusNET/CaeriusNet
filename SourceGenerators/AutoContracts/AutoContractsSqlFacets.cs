using System.Globalization;

namespace CaeriusNet.Generator.AutoContracts;

internal static class AutoContractsSqlFacets
{
    internal static string BuildParameterArguments(AutoContractsParameter parameter)
    {
        var normalized = AutoContractsSqlEmitter.NormalizeSqlType(parameter.SqlType);
        var parts = new List<string>(3);

        if (parameter.MaxLength is { } maxLength && IsSizedType(normalized))
        {
            var size = ToSqlClientSize(normalized, maxLength);
            parts.Add("size: " + size.ToString(CultureInfo.InvariantCulture));
        }

        if (parameter.Precision is { } precision && IsDecimalType(normalized))
            parts.Add("precision: " + precision.ToString(CultureInfo.InvariantCulture));

        if (parameter.Scale is { } scale && IsScaledType(normalized))
            parts.Add("scale: " + scale.ToString(CultureInfo.InvariantCulture));

        return string.Join(", ", parts);
    }

    internal static string BuildColumnLength(AutoContractsColumn column, bool divideByTwo)
    {
        if (column.MaxLength is null or -1)
            return "SqlMetaData.Max";

        var length = column.MaxLength.Value;
        if (divideByTwo)
            length /= 2;

        return Math.Max(length, 1).ToString(CultureInfo.InvariantCulture);
    }

    private static int ToSqlClientSize(string normalizedSqlType, int maxLength)
    {
        var size = maxLength;
        if (normalizedSqlType is "nvarchar" or "nchar" && size > 0)
            size /= 2;

        return Math.Max(size, -1);
    }

    private static bool IsSizedType(string normalizedSqlType)
    {
        return normalizedSqlType is "nvarchar" or "nchar" or "varchar" or "char" or "varbinary" or "binary";
    }

    private static bool IsDecimalType(string normalizedSqlType)
    {
        return normalizedSqlType is "decimal" or "numeric";
    }

    private static bool IsScaledType(string normalizedSqlType)
    {
        return normalizedSqlType is "decimal" or "numeric" or "time" or "datetime2" or "datetimeoffset";
    }
}
