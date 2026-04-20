namespace CaeriusNet.Generator.Helpers;

/// <summary>
///     Symbol-level structural validator shared by the DTO and TVP generators. Walks every
///     <see cref="ISymbol.DeclaringSyntaxReferences" /> so partial declarations split across files do not
///     produce false-positive diagnostics.
/// </summary>
internal static class TypeStructureValidator
{
    /// <summary>
    ///     Result of <see cref="Validate" />. <see cref="PrimaryConstructorDeclaration" /> is non-null when
    ///     any partial declaration of the type carries a primary constructor with at least one parameter.
    /// </summary>
    internal readonly record struct ValidationResult(
        bool IsSealed,
        bool IsPartial,
        TypeDeclarationSyntax? PrimaryConstructorDeclaration);

    /// <summary>
    ///     Validates the structural requirements expected of every CaeriusNet generator target type:
    ///     declared <c>sealed</c>, <c>partial</c>, with a primary constructor exposing at least one parameter.
    /// </summary>
    /// <param name="typeSymbol">The semantic symbol resolved from a candidate syntax node.</param>
    /// <returns>An aggregate <see cref="ValidationResult" /> usable by callers to emit diagnostics.</returns>
    internal static ValidationResult Validate(INamedTypeSymbol typeSymbol)
    {
        var isSealed = typeSymbol.IsSealed;
        var isPartial = false;
        TypeDeclarationSyntax? primaryCtorDeclaration = null;

        foreach (var declRef in typeSymbol.DeclaringSyntaxReferences)
        {
            if (declRef.GetSyntax() is not TypeDeclarationSyntax decl)
                continue;

            if (!isPartial)
                foreach (var modifier in decl.Modifiers)
                    if (modifier.IsKind(SyntaxKind.PartialKeyword))
                    {
                        isPartial = true;
                        break;
                    }

            if (primaryCtorDeclaration is null)
            {
                var paramList = decl switch
                {
                    RecordDeclarationSyntax r => r.ParameterList,
                    ClassDeclarationSyntax c => c.ParameterList,
                    _ => null
                };

                if (paramList is { Parameters.Count: > 0 })
                    primaryCtorDeclaration = decl;
            }
        }

        return new ValidationResult(isSealed, isPartial, primaryCtorDeclaration);
    }

    /// <summary>
    ///     Returns the most useful <see cref="Location" /> for diagnostics: the identifier of the first
    ///     declaration when available, otherwise <see cref="Location.None" />.
    /// </summary>
    internal static Location GetIdentifierLocation(INamedTypeSymbol typeSymbol)
    {
        foreach (var declRef in typeSymbol.DeclaringSyntaxReferences)
            if (declRef.GetSyntax() is TypeDeclarationSyntax decl)
                return decl.Identifier.GetLocation();

        return Location.None;
    }
}
