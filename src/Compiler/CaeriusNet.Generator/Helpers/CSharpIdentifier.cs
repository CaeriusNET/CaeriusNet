namespace CaeriusNet.Generator.Helpers;

/// <summary>
///     Formats identifiers originating from Roslyn symbols for generated C# source.
/// </summary>
/// <remarks>
///     <see cref="ISymbol.Name" /> intentionally omits the source <c>@</c> escape.  Generated source must
///     restore it for reserved and contextual keywords while leaving valid Unicode identifiers untouched.
/// </remarks>
internal static class CSharpIdentifier
{
    internal static string Escape(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            throw new ArgumentException("An identifier is required.", nameof(identifier));

        if (SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ||
            SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None)
            return "@" + identifier;

        if (!SyntaxFacts.IsValidIdentifier(identifier))
            throw new ArgumentException("The Roslyn symbol name is not a valid C# identifier.", nameof(identifier));

        return identifier;
    }
}
