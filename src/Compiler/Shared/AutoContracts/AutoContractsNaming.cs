namespace CaeriusNet.Analyzer.AutoContracts;

/// <summary>
///     Canonical SQL and C# name handling shared by the AutoContracts analyzer and source generator.
///     This source is linked into both components; neither assembly references the other.
/// </summary>
internal static class AutoContractsNaming
{
    internal static string BuildSqlName(string schema, string name)
    {
        return schema + "." + name;
    }

    internal static string BuildSqlName(string schema, string name, string member)
    {
        return BuildSqlName(schema, name) + "." + member;
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

    internal static bool IsSupportedSqlType(string sqlType)
    {
        return NormalizeSqlType(sqlType) switch
        {
            "bit" or "tinyint" or "smallint" or "int" or "bigint" or
                "real" or "float" or "decimal" or "numeric" or "money" or "smallmoney" or
                "date" or "time" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset" or
                "uniqueidentifier" or "nvarchar" or "varchar" or "nchar" or "char" or
                "varbinary" or "binary" or "json" or "vector" => true,
            _ => false
        };
    }

    internal static string ToIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Value";

        var builder = new StringBuilder(name.Length + 1);
        if (!IsIdentifierStart(name[0]))
            builder.Append('_');

        foreach (var ch in name)
            builder.Append(IsIdentifierPart(ch) ? ch : '_');

        var identifier = builder.ToString();
        return IsKeyword(identifier) ? "@" + identifier : identifier;
    }

    internal static bool IsValidIdentifier(string identifier)
    {
        return SyntaxFacts.IsValidIdentifier(identifier) &&
               SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None &&
               SyntaxFacts.GetContextualKeywordKind(identifier) == SyntaxKind.None;
    }

    internal static bool IsValidNamespace(string @namespace)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
            return false;

        foreach (var part in @namespace.Split('.'))
            if (!IsValidIdentifier(part))
                return false;

        return true;
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
        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None;
    }
}
