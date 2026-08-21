namespace CaeriusNet.Analyzer.Roslyn;

/// <summary>
///     Structural validation shared, as linked source, by the CaeriusNet analyzer and source generator.
/// </summary>
internal static class TypeStructureValidator
{
    internal static ValidationResult Validate(INamedTypeSymbol typeSymbol)
    {
        var declarations = typeSymbol.DeclaringSyntaxReferences
            .Select(static reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .OrderBy(static declaration => declaration.SyntaxTree.FilePath, StringComparer.Ordinal)
            .ThenBy(static declaration => declaration.SpanStart)
            .ToImmutableArray();

        var isPartial = declarations.Any(static declaration =>
            declaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));
        var primaryConstructorDeclaration = declarations.FirstOrDefault(HasPrimaryConstructor);

        return new ValidationResult(
            typeSymbol.IsSealed,
            isPartial,
            typeSymbol.ContainingType is null,
            typeSymbol.TypeParameters.Length == 0,
            primaryConstructorDeclaration,
            SelectPrimaryConstructor(typeSymbol, primaryConstructorDeclaration));
    }

    internal static Location GetIdentifierLocation(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.DeclaringSyntaxReferences
            .Select(static reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .OrderBy(static declaration => declaration.SyntaxTree.FilePath, StringComparer.Ordinal)
            .ThenBy(static declaration => declaration.SpanStart)
            .Select(static declaration => declaration.Identifier.GetLocation())
            .FirstOrDefault() ?? Location.None;
    }

    private static bool HasPrimaryConstructor(TypeDeclarationSyntax declaration)
    {
        return declaration switch
        {
            RecordDeclarationSyntax record => record.ParameterList is { Parameters.Count: > 0 },
            ClassDeclarationSyntax @class => @class.ParameterList is { Parameters.Count: > 0 },
            _ => false
        };
    }

    private static IMethodSymbol? SelectPrimaryConstructor(
        INamedTypeSymbol typeSymbol,
        TypeDeclarationSyntax? declaration)
    {
        if (declaration is null)
            return null;

        var parameterCount = declaration switch
        {
            RecordDeclarationSyntax record => record.ParameterList?.Parameters.Count,
            ClassDeclarationSyntax @class => @class.ParameterList?.Parameters.Count,
            _ => null
        };
        if (parameterCount is null)
            return null;
        var requiredParameterCount = parameterCount.Value;

        var candidates = typeSymbol.Constructors
            .Where(constructor => !constructor.IsStatic && constructor.Parameters.Length == requiredParameterCount)
            .OrderBy(static constructor => constructor.Locations.FirstOrDefault()?.SourceTree?.FilePath,
                StringComparer.Ordinal)
            .ThenBy(static constructor => constructor.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue)
            .ToImmutableArray();

        foreach (var candidate in candidates)
        foreach (var reference in candidate.DeclaringSyntaxReferences)
            if (reference.SyntaxTree == declaration.SyntaxTree && reference.Span == declaration.Span)
                return candidate;

        return candidates.FirstOrDefault();
    }

    internal readonly record struct ValidationResult(
        bool IsSealed,
        bool IsPartial,
        bool IsTopLevel,
        bool IsNonGeneric,
        TypeDeclarationSyntax? PrimaryConstructorDeclaration,
        IMethodSymbol? PrimaryConstructor);
}
