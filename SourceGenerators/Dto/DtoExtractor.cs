namespace CaeriusNet.Generator.Dto;

/// <summary>
///     Semantic-phase extractor for <c>[GenerateDto]</c> targets.
/// </summary>
internal static class DtoExtractor
{
    internal static DtoModel? Extract(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol ||
            context.TargetNode is not TypeDeclarationSyntax)
            return null;

        var validation = TypeStructureValidator.Validate(typeSymbol);
        if (!validation.IsSealed || !validation.IsPartial || validation.PrimaryConstructorDeclaration is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        var primaryCtorDecl = validation.PrimaryConstructorDeclaration!;
        var columns = ExtractColumns(typeSymbol, primaryCtorDecl);

        if (columns.Count == 0)
            return null;

        var typeKindKeyword = primaryCtorDecl.Kind() == SyntaxKind.ClassDeclaration ? "class" : "record";
        return new DtoModel(
            NamespaceHelper.GetNamespace(typeSymbol),
            typeSymbol.Name,
            typeKindKeyword,
            columns);
    }

    /// <summary>
    ///     Resolves the primary-constructor parameters as <see cref="ColumnModel" />s.
    /// </summary>
    private static EquatableArray<ColumnModel> ExtractColumns(
        INamedTypeSymbol typeSymbol,
        TypeDeclarationSyntax primaryCtorDecl)
    {
        var parameterList = primaryCtorDecl switch
        {
            RecordDeclarationSyntax r => r.ParameterList,
            ClassDeclarationSyntax c => c.ParameterList,
            _ => null
        };

        if (parameterList is null || parameterList.Parameters.Count == 0)
            return EquatableArray<ColumnModel>.Empty;

        var primaryConstructor =
            typeSymbol.Constructors.FirstOrDefault(c =>
                !c.IsStatic && c.Parameters.Length == parameterList.Parameters.Count);
        if (primaryConstructor is null)
            return EquatableArray<ColumnModel>.Empty;

        var builder = ImmutableArray.CreateBuilder<ColumnModel>(primaryConstructor.Parameters.Length);
        for (var i = 0; i < primaryConstructor.Parameters.Length; i++)
        {
            var parameter = primaryConstructor.Parameters[i];
            builder.Add(ColumnExtractor.Extract(parameter, i));
        }

        return new EquatableArray<ColumnModel>(builder.ToImmutable());
    }
}