namespace CaeriusNet.Generator.Tvp;

/// <summary>
///     Semantic-phase extractor for <c>[GenerateTvp]</c> targets.
/// </summary>
internal static class TvpExtractor
{
    private const string AttributeDisplayName = "[GenerateTvp]";
    private const string GenerateTvpAttributeShortName = "GenerateTvpAttribute";

    internal static ExtractionResult<TvpModel> Extract(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol ||
            context.TargetNode is not TypeDeclarationSyntax declaration)
            return ExtractionResult<TvpModel>.None;

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

        var (schema, tvpName, tvpNameDiag) = ExtractAttribute(context, typeSymbol.Name, locationInfo);
        if (tvpNameDiag is not null)
        {
            diagnostics.Add(tvpNameDiag);
            hasFatal = true;
        }

        if (hasFatal || tvpName is null)
            return new ExtractionResult<TvpModel>(null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));

        cancellationToken.ThrowIfCancellationRequested();

        var primaryCtorDecl = validation.PrimaryConstructorDeclaration!;
        var columns = ExtractColumns(typeSymbol, primaryCtorDecl, diagnostics);

        if (columns.Count == 0)
            return new ExtractionResult<TvpModel>(null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));

        var typeKindKeyword = primaryCtorDecl.Kind() == SyntaxKind.ClassDeclaration ? "class" : "record";
        var model = new TvpModel(
            NamespaceHelper.GetNamespace(typeSymbol),
            typeSymbol.Name,
            typeKindKeyword,
            schema,
            tvpName,
            columns);

        return new ExtractionResult<TvpModel>(model, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
    }

    private static (string Schema, string? TvpName, DiagnosticInfo? Diagnostic) ExtractAttribute(
        GeneratorAttributeSyntaxContext context,
        string ownerTypeName,
        LocationInfo? fallbackLocation)
    {
        var attribute = context.Attributes.FirstOrDefault(static attr =>
            attr.AttributeClass?.Name == GenerateTvpAttributeShortName);

        if (attribute is null)
            return ("dbo", null, null);

        var schemaArg = attribute.NamedArguments.FirstOrDefault(static na => na.Key == "Schema");
        var schema = schemaArg.Value.Value is string s && !string.IsNullOrWhiteSpace(s) ? s : "dbo";

        var tvpNameArg = attribute.NamedArguments.FirstOrDefault(static na => na.Key == "TvpName");
        var tvpName = tvpNameArg.Value.Value as string;

        if (!string.IsNullOrWhiteSpace(tvpName))
            return (schema, tvpName, null);

        var attributeLocation = attribute.ApplicationSyntaxReference is { } syntaxRef
            ? LocationInfo.CreateFrom(syntaxRef.GetSyntax().GetLocation())
            : fallbackLocation;

        return (schema, null, DiagnosticInfo.Create(
            DiagnosticDescriptors.TvpNameMustNotBeEmpty, attributeLocation, ownerTypeName));
    }

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