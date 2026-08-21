namespace CaeriusNet.Generator.Tvp;

/// <summary>
///     Semantic-phase extractor for <c>[GenerateTvp]</c> targets.
/// </summary>
internal static class TvpExtractor
{
    private const string GenerateTvpAttributeShortName = "GenerateTvpAttribute";

    internal static TvpExtractionResult Extract(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol ||
            context.TargetNode is not TypeDeclarationSyntax)
            return TvpExtractionResult.Empty;

        var validation = TypeStructureValidator.Validate(typeSymbol);
        if (!validation.IsPartial || !validation.IsTopLevel || !validation.IsNonGeneric ||
            !IsSupportedTvpShape(typeSymbol) ||
            validation.PrimaryConstructorDeclaration is null)
            return TvpExtractionResult.Empty;

        var (schema, tvpName) = ExtractAttribute(context);
        if (tvpName is null)
            return TvpExtractionResult.Empty;

        cancellationToken.ThrowIfCancellationRequested();

        var primaryCtorDecl = validation.PrimaryConstructorDeclaration!;
        var columnValidation = ManualTvpColumnValidator.Validate(validation.PrimaryConstructor!, cancellationToken);
        if (columnValidation.Issues.Count != 0)
            return new TvpExtractionResult(null, columnValidation.Issues);

        var columns = ExtractColumns(validation.PrimaryConstructor!, columnValidation.Columns, cancellationToken);

        if (columns.Count == 0)
            return TvpExtractionResult.Empty;

        var declarationKeywords = typeSymbol.TypeKind == TypeKind.Struct
            ? "readonly partial record struct"
            : "sealed partial record";
        return new TvpExtractionResult(
            new TvpModel(
                NamespaceHelper.GetNamespace(typeSymbol),
                typeSymbol.Name,
                GetAccessibilityKeyword(typeSymbol.DeclaredAccessibility),
                declarationKeywords,
                schema,
                tvpName,
                columns),
            EquatableArray<ManualTvpColumnIssue>.Empty);
    }

    private static bool IsSupportedTvpShape(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.IsRecord &&
               ((typeSymbol.TypeKind == TypeKind.Class && typeSymbol.IsSealed) ||
                (typeSymbol.TypeKind == TypeKind.Struct && typeSymbol.IsReadOnly));
    }

    private static string GetAccessibilityKeyword(Accessibility accessibility)
    {
        return accessibility == Accessibility.Public ? "public" : "internal";
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
        IMethodSymbol primaryConstructor,
        EquatableArray<ManualTvpColumnContract> contracts,
        CancellationToken cancellationToken)
    {
        if (primaryConstructor.Parameters.Length == 0 || contracts.Count != primaryConstructor.Parameters.Length)
            return EquatableArray<ColumnModel>.Empty;

        var builder = ImmutableArray.CreateBuilder<ColumnModel>(primaryConstructor.Parameters.Length);
        foreach (var contract in contracts.OrderBy(static contract => contract.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (contract.ParameterIndex < 0 || contract.ParameterIndex >= primaryConstructor.Parameters.Length)
                return EquatableArray<ColumnModel>.Empty;

            var parameter = primaryConstructor.Parameters[contract.ParameterIndex];
            var column = ManualTvpColumnMapper.Create(parameter, contract);
            if (column is null)
                return EquatableArray<ColumnModel>.Empty;
            builder.Add(column);
        }

        return new EquatableArray<ColumnModel>(builder.ToImmutable());
    }
}

/// <summary>Value-equatable result of manual TVP extraction, including blocking generator diagnostics.</summary>
internal sealed record TvpExtractionResult(
    TvpModel? Model,
    EquatableArray<ManualTvpColumnIssue> Issues)
{
    internal static TvpExtractionResult Empty { get; } = new(null, EquatableArray<ManualTvpColumnIssue>.Empty);
}
