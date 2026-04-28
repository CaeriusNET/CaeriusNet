namespace CaeriusNet.Analyzer.Helpers;

/// <summary>
///     Symbol-level structural validator shared by analyzer rules that target generator-annotated types.
/// </summary>
/// <remarks>
///     This logic intentionally mirrors the generator-side validator instead of referencing generator code
///     so the analyzer stays independently loadable as a Roslyn component with no circular project dependency.
/// </remarks>
internal static class TypeStructureValidator
{
    internal static ValidationResult Validate(INamedTypeSymbol typeSymbol)
    {
        var isSealed = typeSymbol.IsSealed;
        var isPartial = false;
        var isTopLevel = typeSymbol.ContainingType is null;
        var isNonGeneric = typeSymbol.TypeParameters.Length == 0;
        TypeDeclarationSyntax? primaryCtorDeclaration = null;

        foreach (var declRef in typeSymbol.DeclaringSyntaxReferences)
        {
            if (declRef.GetSyntax() is not TypeDeclarationSyntax decl)
                continue;

            if (!isPartial)
                foreach (var modifier in decl.Modifiers)
                {
                    if (!modifier.IsKind(SyntaxKind.PartialKeyword))
                        continue;

                    isPartial = true;
                    break;
                }

            if (primaryCtorDeclaration is not null)
                continue;

            var parameterList = decl switch
            {
                RecordDeclarationSyntax recordDeclaration => recordDeclaration.ParameterList,
                ClassDeclarationSyntax classDeclaration => classDeclaration.ParameterList,
                _ => null
            };

            if (parameterList is { Parameters.Count: > 0 })
                primaryCtorDeclaration = decl;
        }

        return new ValidationResult(isSealed, isPartial, isTopLevel, isNonGeneric, primaryCtorDeclaration);
    }

    internal static Location GetIdentifierLocation(INamedTypeSymbol typeSymbol)
    {
        foreach (var declRef in typeSymbol.DeclaringSyntaxReferences)
            if (declRef.GetSyntax() is TypeDeclarationSyntax decl)
                return decl.Identifier.GetLocation();

        return Location.None;
    }

    internal readonly struct ValidationResult
    {
        internal ValidationResult(
            bool isSealed,
            bool isPartial,
            bool isTopLevel,
            bool isNonGeneric,
            TypeDeclarationSyntax? primaryConstructorDeclaration)
        {
            IsSealed = isSealed;
            IsPartial = isPartial;
            IsTopLevel = isTopLevel;
            IsNonGeneric = isNonGeneric;
            PrimaryConstructorDeclaration = primaryConstructorDeclaration;
        }

        internal bool IsSealed { get; }

        internal bool IsPartial { get; }

        internal bool IsTopLevel { get; }

        internal bool IsNonGeneric { get; }

        internal TypeDeclarationSyntax? PrimaryConstructorDeclaration { get; }
    }
}
