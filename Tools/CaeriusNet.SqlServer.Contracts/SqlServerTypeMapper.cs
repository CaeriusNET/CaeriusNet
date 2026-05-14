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
            "decimal" or "numeric" or "money" or "smallmoney" => "decimal",
            "date" => "DateOnly",
            "time" => "TimeOnly",
            "datetime" or "datetime2" or "smalldatetime" => "DateTime",
            "datetimeoffset" => "DateTimeOffset",
            "uniqueidentifier" => "Guid",
            "nvarchar" or "varchar" or "nchar" or "char" => "string",
            "binary" or "varbinary" => "byte[]",
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
                "varbinary" or "binary" => true,
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
}