namespace CaeriusNet.Generator.Dto;

/// <summary>
///     Semantic-phase extractor for <c>[GenerateDto]</c> targets. Returns a value-equatable
///     <see cref="ExtractionResult{TModel}" /> so the result flows safely through Roslyn's incremental cache.
/// </summary>
internal static class DtoExtractor
{
    private const string AttributeDisplayName = "[GenerateDto]";

    internal static ExtractionResult<DtoModel> Extract(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol ||
            context.TargetNode is not TypeDeclarationSyntax declaration)
            return ExtractionResult<DtoModel>.None;

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var locationInfo = LocationInfo.CreateFrom(declaration.Identifier.GetLocation());
        var hasFatal = false;

        var validation = TypeStructureValidator.Validate(typeSymbol);

        if (!validation.IsSealed)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.MustBeSealed, locationInfo, typeSymbol.Name, AttributeDisplayName));
            hasFatal = true;
        }

        if (!validation.IsPartial)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.MustBePartial, locationInfo, typeSymbol.Name, AttributeDisplayName));
            hasFatal = true;
        }

        if (validation.PrimaryConstructorDeclaration is null)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.MustHavePrimaryConstructor, locationInfo, typeSymbol.Name, AttributeDisplayName));
            hasFatal = true;
        }

        if (hasFatal)
            return new ExtractionResult<DtoModel>(null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));

        cancellationToken.ThrowIfCancellationRequested();

        var primaryCtorDecl = validation.PrimaryConstructorDeclaration!;
        var columns = ExtractColumns(typeSymbol, primaryCtorDecl, diagnostics);

        if (columns.Count == 0)
            return new ExtractionResult<DtoModel>(null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));

        var typeKindKeyword = primaryCtorDecl.Kind() == SyntaxKind.ClassDeclaration ? "class" : "record";
        var model = new DtoModel(
            NamespaceHelper.GetNamespace(typeSymbol),
            typeSymbol.Name,
            typeKindKeyword,
            columns);

        return new ExtractionResult<DtoModel>(model, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
    }

    /// <summary>
    ///     Resolves the primary-constructor parameters as <see cref="ColumnModel" />s and emits CAERIUS005
    ///     for any parameter whose type falls back to <c>sql_variant</c>.
    /// </summary>
    private static EquatableArray<ColumnModel> ExtractColumns(
        INamedTypeSymbol typeSymbol,
        TypeDeclarationSyntax primaryCtorDecl,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
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
            var column = ColumnExtractor.Extract(parameter, i);
            builder.Add(column);

            if (column.SqlType != "sql_variant") continue;
            var paramSyntax = i < parameterList.Parameters.Count ? parameterList.Parameters[i] : null;
            var paramLocation = paramSyntax is not null
                ? LocationInfo.CreateFrom(paramSyntax.GetLocation())
                : LocationInfo.CreateFrom(primaryCtorDecl.Identifier.GetLocation());

            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.UnsupportedSqlMapping,
                paramLocation,
                parameter.Name, column.TypeName, typeSymbol.Name));
        }

        return new EquatableArray<ColumnModel>(builder.ToImmutable());
    }
}