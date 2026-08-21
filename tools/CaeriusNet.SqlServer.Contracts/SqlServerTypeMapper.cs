namespace CaeriusNet.SqlServer.Contracts;

internal static class SqlServerTypeMapper
{
    internal static string GetClrType(string sqlType, bool nullable)
    {
        var clrType = Normalize(sqlType) switch
        {
            "bit" => "bool",
            "tinyint" => "byte",
            "smallint" => "short",
            "int" => "int",
            "bigint" => "long",
            "real" => "float",
            "float" => "double",
            "decimal" or "numeric" when GetPrecision(sqlType) > 28 => "System.Data.SqlTypes.SqlDecimal",
            "decimal" or "numeric" or "money" or "smallmoney" => "decimal",
            "date" => "DateOnly",
            "time" => "TimeOnly",
            "datetime" or "datetime2" or "smalldatetime" => "DateTime",
            "datetimeoffset" => "DateTimeOffset",
            "uniqueidentifier" => "Guid",
            "nvarchar" or "varchar" or "nchar" or "char" => "string",
            "binary" or "varbinary" => "byte[]",
            "json" => "string",
            "vector" => "global::Microsoft.Data.SqlTypes.SqlVector<float>",
            _ => "object"
        };

        if (!nullable)
            return clrType;

        return clrType + "?";
    }

    internal static bool IsSupported(string sqlType)
    {
        return Normalize(sqlType) switch
        {
            "bit" or "tinyint" or "smallint" or "int" or "bigint" or
                "real" or "float" or "decimal" or "numeric" or "money" or "smallmoney" or
                "date" or "time" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset" or
                "uniqueidentifier" or "nvarchar" or "varchar" or "nchar" or "char" or
                "varbinary" or "binary" or "json" or "vector" => true,
            _ => false
        };
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

    private static int GetPrecision(string sqlType)
    {
        var open = sqlType.IndexOf('(', StringComparison.Ordinal);
        if (open < 0)
            return 0;
        var commaOffset = sqlType.AsSpan(open + 1).IndexOf(',');
        if (commaOffset < 0)
            return 0;
        var comma = open + 1 + commaOffset;
        return int.TryParse(sqlType.AsSpan(open + 1, comma - open - 1), out var precision)
            ? precision
            : 0;
    }
}
