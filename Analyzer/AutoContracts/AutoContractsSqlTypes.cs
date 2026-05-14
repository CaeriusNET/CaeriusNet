namespace CaeriusNet.Analyzer.AutoContracts;

internal static class AutoContractsSqlTypes
{
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

    internal static string Normalize(string sqlType)
    {
        var span = sqlType.AsSpan().Trim();
        var paren = span.IndexOf('(');
        if (paren >= 0)
            span = span.Slice(0, paren);

        var dot = span.LastIndexOf('.');
        if (dot >= 0)
            span = span.Slice(dot + 1);

        return span.ToString().ToLowerInvariant();
    }
}