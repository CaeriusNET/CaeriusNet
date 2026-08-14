using System.Globalization;

namespace CaeriusNet.SqlServer.Contracts;

internal static class SqlServerTypeName
{
    internal static string Format(string sqlType, int maxLength, byte precision, byte scale)
    {
        var normalized = Normalize(sqlType);
        return normalized switch
        {
            "nvarchar" or "nchar" => normalized + "(" + FormatLength(maxLength, true) + ")",
            "varchar" or "char" or "varbinary" or "binary" =>
                normalized + "(" + FormatLength(maxLength, false) + ")",
            "decimal" or "numeric" => normalized + "(" + precision + "," + scale + ")",
            "time" or "datetime2" or "datetimeoffset" => normalized + "(" + scale + ")",
            _ => normalized
        };
    }

    private static string FormatLength(int maxLength, bool divideByTwo)
    {
        if (maxLength == -1)
            return "max";

        var length = divideByTwo ? maxLength / 2 : maxLength;
        return Math.Max(length, 1).ToString(CultureInfo.InvariantCulture);
    }

    private static string Normalize(string sqlType)
    {
        var span = sqlType.AsSpan().Trim();
        var paren = span.IndexOf('(');
        if (paren >= 0)
            span = span[..paren];

        var dot = span.LastIndexOf('.');
        if (dot >= 0)
            span = span[(dot + 1)..];

        return span.ToString().ToLowerInvariant();
    }
}
