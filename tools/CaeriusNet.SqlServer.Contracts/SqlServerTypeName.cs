using System.Globalization;

namespace CaeriusNet.SqlServer.Contracts;

internal static class SqlServerTypeName
{
    internal static string Format(
        string sqlType,
        int maxLength,
        byte precision,
        byte scale,
        int? vectorDimensions = null)
    {
        var normalized = Normalize(sqlType);
        return normalized switch
        {
            "nvarchar" or "nchar" => normalized + "(" + FormatLength(maxLength, true) + ")",
            "varchar" or "char" or "varbinary" or "binary" =>
                normalized + "(" + FormatLength(maxLength, false) + ")",
            "decimal" or "numeric" => normalized + "(" + precision + "," + scale + ")",
            "time" or "datetime2" or "datetimeoffset" => normalized + "(" + scale + ")",
            // SQL Server 2025 exposes vector_dimensions explicitly.  max_length is storage metadata,
            // not a documented vector contract and must never be used to infer a dimension.
            "vector" when vectorDimensions is { } dimensions =>
                normalized + "(" + dimensions.ToString(CultureInfo.InvariantCulture) + ")",
            "vector" when TryGetVectorDimensions(sqlType, out var dimensions) =>
                normalized + "(" + dimensions.ToString(CultureInfo.InvariantCulture) + ")",
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

    private static bool TryGetVectorDimensions(string sqlType, out int dimensions)
    {
        dimensions = 0;
        var span = sqlType.AsSpan().Trim();
        var open = span.IndexOf('(');
        if (open < 0 || span.Length < open + 3 || span[span.Length - 1] != ')')
            return false;
        if (!string.Equals(Normalize(span.Slice(0, open).ToString()), "vector", StringComparison.Ordinal))
            return false;

        var text = span.Slice(open + 1, span.Length - open - 2).Trim().ToString();
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out dimensions) &&
               dimensions is >= 1 and <= 1998;
    }
}
