namespace CaeriusNet.Generator.Tvp;

/// <summary>
///     Semantic-phase extractor for <c>[GenerateTvp]</c> targets.
/// </summary>
internal static class TvpExtractor
{
    private const string GenerateTvpAttributeShortName = "GenerateTvpAttribute";

    internal static TvpModel? Extract(
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

        var (schema, tvpName) = ExtractAttribute(context);
        if (tvpName is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        var primaryCtorDecl = validation.PrimaryConstructorDeclaration!;
        var columns = ExtractColumns(typeSymbol, primaryCtorDecl);

        if (columns.Count == 0)
            return null;

        var typeKindKeyword = primaryCtorDecl.Kind() == SyntaxKind.ClassDeclaration ? "class" : "record";
        return new TvpModel(
            NamespaceHelper.GetNamespace(typeSymbol),
            typeSymbol.Name,
            typeKindKeyword,
            schema,
            tvpName,
            columns);
    }

    private static (string Schema, string? TvpName) ExtractAttribute(
        GeneratorAttributeSyntaxContext context)
    {
        var attribute = context.Attributes.FirstOrDefault(static attr =>
            attr.AttributeClass?.Name == GenerateTvpAttributeShortName);

        if (attribute is null)
            return ("dbo", null);

        var schemaArg = attribute.NamedArguments.FirstOrDefault(static na => na.Key == "Schema");
        var schema = schemaArg.Value.Value is string s && !string.IsNullOrWhiteSpace(s) ? s : "dbo";

        var tvpNameArg = attribute.NamedArguments.FirstOrDefault(static na => na.Key == "TvpName");
        var tvpName = tvpNameArg.Value.Value as string;

        return !string.IsNullOrWhiteSpace(tvpName)
            ? (schema, tvpName)
            : (schema, null);
    }

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