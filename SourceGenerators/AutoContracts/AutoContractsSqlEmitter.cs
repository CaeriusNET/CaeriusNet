namespace CaeriusNet.Generator.AutoContracts;

internal static class AutoContractsSqlEmitter
{
    internal static string BuildSqlDbTypeExpression(string sqlType)
    {
        return NormalizeSqlType(sqlType) switch
        {
            "bit" => "SqlDbType.Bit",
            "tinyint" => "SqlDbType.TinyInt",
            "smallint" => "SqlDbType.SmallInt",
            "int" => "SqlDbType.Int",
            "bigint" => "SqlDbType.BigInt",
            "real" => "SqlDbType.Real",
            "float" => "SqlDbType.Float",
            "decimal" => "SqlDbType.Decimal",
            "numeric" => "SqlDbType.Decimal",
            "money" => "SqlDbType.Money",
            "smallmoney" => "SqlDbType.SmallMoney",
            "date" => "SqlDbType.Date",
            "time" => "SqlDbType.Time",
            "datetime" => "SqlDbType.DateTime",
            "datetime2" => "SqlDbType.DateTime2",
            "smalldatetime" => "SqlDbType.SmallDateTime",
            "datetimeoffset" => "SqlDbType.DateTimeOffset",
            "uniqueidentifier" => "SqlDbType.UniqueIdentifier",
            "nvarchar" => "SqlDbType.NVarChar",
            "varchar" => "SqlDbType.VarChar",
            "nchar" => "SqlDbType.NChar",
            "char" => "SqlDbType.Char",
            "varbinary" => "SqlDbType.VarBinary",
            "binary" => "SqlDbType.Binary",
            _ => "SqlDbType.Variant"
        };
    }

    internal static string BuildReaderExpression(AutoContractsColumn column, int readerOrdinal)
    {
        var nullablePrefix = column.Nullable ? $"reader.IsDBNull({readerOrdinal}) ? null : " : string.Empty;
        var normalized = NormalizeSqlType(column.SqlType);

        return normalized switch
        {
            "bit" => $"{nullablePrefix}reader.GetBoolean({readerOrdinal})",
            "tinyint" => $"{nullablePrefix}reader.GetByte({readerOrdinal})",
            "smallint" => $"{nullablePrefix}reader.GetInt16({readerOrdinal})",
            "int" => $"{nullablePrefix}reader.GetInt32({readerOrdinal})",
            "bigint" => $"{nullablePrefix}reader.GetInt64({readerOrdinal})",
            "real" => $"{nullablePrefix}reader.GetFloat({readerOrdinal})",
            "float" => $"{nullablePrefix}reader.GetDouble({readerOrdinal})",
            "decimal" or "numeric" or "money" or "smallmoney" =>
                $"{nullablePrefix}reader.GetDecimal({readerOrdinal})",
            "date" => $"{nullablePrefix}DateOnly.FromDateTime(reader.GetDateTime({readerOrdinal}))",
            "time" => $"{nullablePrefix}TimeOnly.FromTimeSpan(reader.GetTimeSpan({readerOrdinal}))",
            "datetime" or "datetime2" or "smalldatetime" =>
                $"{nullablePrefix}reader.GetDateTime({readerOrdinal})",
            "datetimeoffset" => $"{nullablePrefix}reader.GetDateTimeOffset({readerOrdinal})",
            "uniqueidentifier" => $"{nullablePrefix}reader.GetGuid({readerOrdinal})",
            "nvarchar" or "varchar" or "nchar" or "char" =>
                $"{nullablePrefix}reader.GetString({readerOrdinal})",
            "varbinary" or "binary" =>
                $"{nullablePrefix}reader.GetFieldValue<byte[]>({readerOrdinal})",
            _ => $"{nullablePrefix}({ToNullableClrType(column)})reader.GetValue({readerOrdinal})"
        };
    }

    internal static string BuildSqlMetaDataExpression(AutoContractsColumn column)
    {
        var columnName = ToStringLiteral(column.Name);
        var normalized = NormalizeSqlType(column.SqlType);

        return normalized switch
        {
            "bit" => $"new SqlMetaData({columnName}, SqlDbType.Bit)",
            "tinyint" => $"new SqlMetaData({columnName}, SqlDbType.TinyInt)",
            "smallint" => $"new SqlMetaData({columnName}, SqlDbType.SmallInt)",
            "int" => $"new SqlMetaData({columnName}, SqlDbType.Int)",
            "bigint" => $"new SqlMetaData({columnName}, SqlDbType.BigInt)",
            "real" => $"new SqlMetaData({columnName}, SqlDbType.Real)",
            "float" => $"new SqlMetaData({columnName}, SqlDbType.Float)",
            "decimal" or "numeric" =>
                $"new SqlMetaData({columnName}, SqlDbType.Decimal, {column.Precision ?? 18}, {column.Scale ?? 0})",
            "money" => $"new SqlMetaData({columnName}, SqlDbType.Money)",
            "smallmoney" => $"new SqlMetaData({columnName}, SqlDbType.SmallMoney)",
            "date" => $"new SqlMetaData({columnName}, SqlDbType.Date)",
            "time" => $"new SqlMetaData({columnName}, SqlDbType.Time, {column.Scale ?? 7})",
            "datetime" => $"new SqlMetaData({columnName}, SqlDbType.DateTime)",
            "datetime2" => $"new SqlMetaData({columnName}, SqlDbType.DateTime2, {column.Scale ?? 7})",
            "smalldatetime" => $"new SqlMetaData({columnName}, SqlDbType.SmallDateTime)",
            "datetimeoffset" => $"new SqlMetaData({columnName}, SqlDbType.DateTimeOffset, {column.Scale ?? 7})",
            "uniqueidentifier" => $"new SqlMetaData({columnName}, SqlDbType.UniqueIdentifier)",
            "nvarchar" =>
                $"new SqlMetaData({columnName}, SqlDbType.NVarChar, {AutoContractsSqlFacets.BuildColumnLength(column, true)})",
            "nchar" =>
                $"new SqlMetaData({columnName}, SqlDbType.NChar, {AutoContractsSqlFacets.BuildColumnLength(column, true)})",
            "varchar" =>
                $"new SqlMetaData({columnName}, SqlDbType.VarChar, {AutoContractsSqlFacets.BuildColumnLength(column, false)})",
            "char" =>
                $"new SqlMetaData({columnName}, SqlDbType.Char, {AutoContractsSqlFacets.BuildColumnLength(column, false)})",
            "varbinary" =>
                $"new SqlMetaData({columnName}, SqlDbType.VarBinary, {AutoContractsSqlFacets.BuildColumnLength(column, false)})",
            "binary" =>
                $"new SqlMetaData({columnName}, SqlDbType.Binary, {AutoContractsSqlFacets.BuildColumnLength(column, false)})",
            _ => $"new SqlMetaData({columnName}, SqlDbType.Variant)"
        };
    }

    internal static string BuildSetExpression(AutoContractsColumn column, int ordinal, string valueExpression)
    {
        var normalized = NormalizeSqlType(column.SqlType);
        var valueAccess = column.Nullable && AutoContractsClrTypes.IsNullableValueType(column.ClrType)
            ? ".Value"
            : string.Empty;
        var value = valueExpression + valueAccess;

        return normalized switch
        {
            "bit" => $"record.SetBoolean({ordinal}, {value})",
            "tinyint" => $"record.SetByte({ordinal}, {value})",
            "smallint" => $"record.SetInt16({ordinal}, {value})",
            "int" => $"record.SetInt32({ordinal}, {value})",
            "bigint" => $"record.SetInt64({ordinal}, {value})",
            "real" => $"record.SetFloat({ordinal}, {value})",
            "float" => $"record.SetDouble({ordinal}, {value})",
            "decimal" or "numeric" or "money" or "smallmoney" => $"record.SetDecimal({ordinal}, {value})",
            "date" => $"record.SetDateTime({ordinal}, {value}.ToDateTime(TimeOnly.MinValue))",
            "time" => $"record.SetTimeSpan({ordinal}, {value}.ToTimeSpan())",
            "datetime" or "datetime2" or "smalldatetime" => $"record.SetDateTime({ordinal}, {value})",
            "datetimeoffset" => $"record.SetDateTimeOffset({ordinal}, {value})",
            "uniqueidentifier" => $"record.SetGuid({ordinal}, {value})",
            "nvarchar" or "varchar" or "nchar" or "char" => $"record.SetString({ordinal}, {value})",
            "varbinary" or "binary" => $"record.SetValue({ordinal}, {value})",
            _ => $"record.SetValue({ordinal}, {value})"
        };
    }

    internal static string ToNullableClrType(AutoContractsColumn column)
    {
        return ApplyNullability(column.ClrType, column.Nullable);
    }

    internal static string ApplyNullability(string clrType, bool nullable)
    {
        if (!nullable || clrType.EndsWith("?", StringComparison.Ordinal))
            return clrType;

        return AutoContractsClrTypes.IsReferenceType(clrType) || AutoContractsClrTypes.IsNullableValueType(clrType)
            ? clrType + "?"
            : clrType + "?";
    }

    internal static bool IsSupportedSqlType(string sqlType)
    {
        return NormalizeSqlType(sqlType) switch
        {
            "bit" or "tinyint" or "smallint" or "int" or "bigint" or
                "real" or "float" or "decimal" or "numeric" or "money" or "smallmoney" or
                "date" or "time" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset" or
                "uniqueidentifier" or "nvarchar" or "varchar" or "nchar" or "char" or
                "varbinary" or "binary" => true,
            _ => false
        };
    }

    internal static string ToIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Value";

        var sb = new StringBuilder(name.Length + 1);
        if (!IsIdentifierStart(name[0]))
            sb.Append('_');

        foreach (var ch in name)
            sb.Append(IsIdentifierPart(ch) ? ch : '_');

        var identifier = sb.ToString();
        return IsKeyword(identifier) ? "@" + identifier : identifier;
    }

    internal static string ToStringLiteral(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var ch in value)
            switch (ch)
            {
                case '\\':
                    sb.Append(@"\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append(@"\n");
                    break;
                case '\r':
                    sb.Append(@"\r");
                    break;
                case '\t':
                    sb.Append(@"\t");
                    break;
                default:
                    if (char.IsControl(ch))
                        sb.Append(@"\u").Append(((int)ch).ToString("x4"));
                    else
                        sb.Append(ch);
                    break;
            }

        sb.Append('"');
        return sb.ToString();
    }

    internal static string NormalizeSqlType(string sqlType)
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

    private static bool IsIdentifierStart(char ch)
    {
        return ch == '_' || char.IsLetter(ch);
    }

    private static bool IsIdentifierPart(char ch)
    {
        return ch == '_' || char.IsLetterOrDigit(ch);
    }

    private static bool IsKeyword(string identifier)
    {
        return identifier is "class" or "struct" or "record" or "namespace" or "public" or "private" or
            "internal" or "readonly" or "string" or "int" or "bool" or "decimal" or "object";
    }
}