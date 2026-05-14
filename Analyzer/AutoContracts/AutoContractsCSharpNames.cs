using System.Text;

namespace CaeriusNet.Analyzer.AutoContracts;

internal static class AutoContractsCSharpNames
{
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
