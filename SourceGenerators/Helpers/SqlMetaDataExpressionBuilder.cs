namespace CaeriusNet.Generator.Helpers;

/// <summary>
///     Builds the C# expression used to construct a <c>SqlMetaData</c> entry for a given column.
/// </summary>
/// <remarks>
///     Centralising the mapping prevents drift between extraction and emission, and makes it trivial to
///     evolve TVP defaults (precision/scale, length policy) in a single place.
/// </remarks>
internal static class SqlMetaDataExpressionBuilder
{
    internal static string Build(string columnName, string sqlType)
    {
        return sqlType switch
        {
            "bit" => $"new SqlMetaData(\"{columnName}\", SqlDbType.Bit)",
            "tinyint" => $"new SqlMetaData(\"{columnName}\", SqlDbType.TinyInt)",
            "smallint" => $"new SqlMetaData(\"{columnName}\", SqlDbType.SmallInt)",
            "int" => $"new SqlMetaData(\"{columnName}\", SqlDbType.Int)",
            "bigint" => $"new SqlMetaData(\"{columnName}\", SqlDbType.BigInt)",
            "decimal" => $"new SqlMetaData(\"{columnName}\", SqlDbType.Decimal, 18, 4)",
            "real" => $"new SqlMetaData(\"{columnName}\", SqlDbType.Real)",
            "float" => $"new SqlMetaData(\"{columnName}\", SqlDbType.Float)",
            "nvarchar" => $"new SqlMetaData(\"{columnName}\", SqlDbType.NVarChar, SqlMetaData.Max)",
            "varchar" => $"new SqlMetaData(\"{columnName}\", SqlDbType.VarChar, SqlMetaData.Max)",
            "nchar" => $"new SqlMetaData(\"{columnName}\", SqlDbType.NChar, 1)",
            "char" => $"new SqlMetaData(\"{columnName}\", SqlDbType.Char, 1)",
            "datetime2" => $"new SqlMetaData(\"{columnName}\", SqlDbType.DateTime2, 7)",
            "datetime" => $"new SqlMetaData(\"{columnName}\", SqlDbType.DateTime)",
            "date" => $"new SqlMetaData(\"{columnName}\", SqlDbType.Date)",
            "time" => $"new SqlMetaData(\"{columnName}\", SqlDbType.Time, 7)",
            "datetimeoffset" => $"new SqlMetaData(\"{columnName}\", SqlDbType.DateTimeOffset, 7)",
            "uniqueidentifier" => $"new SqlMetaData(\"{columnName}\", SqlDbType.UniqueIdentifier)",
            "varbinary" => $"new SqlMetaData(\"{columnName}\", SqlDbType.VarBinary, SqlMetaData.Max)",
            _ => $"new SqlMetaData(\"{columnName}\", SqlDbType.Variant)"
        };
    }
}