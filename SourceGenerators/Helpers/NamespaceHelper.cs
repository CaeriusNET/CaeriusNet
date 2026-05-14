namespace CaeriusNet.Generator.Helpers;

/// <summary>
///     Resolves the namespace string for a target type, returning the empty string for the global namespace.
/// </summary>
internal static class NamespaceHelper
{
    internal static string GetNamespace(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();
    }
}