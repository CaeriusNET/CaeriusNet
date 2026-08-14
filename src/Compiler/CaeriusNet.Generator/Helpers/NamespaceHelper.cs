namespace CaeriusNet.Generator.Helpers;

/// <summary>
///     Resolves the namespace string for a target type, returning the empty string for the global namespace.
/// </summary>
internal static class NamespaceHelper
{
    internal static string GetNamespace(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.ContainingNamespace.IsGlobalNamespace)
            return string.Empty;

        var segments = new Stack<string>();
        for (var current = typeSymbol.ContainingNamespace;
             !current.IsGlobalNamespace;
             current = current.ContainingNamespace)
            segments.Push(CSharpIdentifier.Escape(current.Name));

        return string.Join(".", segments);
    }
}
