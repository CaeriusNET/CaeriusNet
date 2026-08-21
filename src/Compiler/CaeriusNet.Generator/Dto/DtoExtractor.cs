namespace CaeriusNet.Generator.Dto;

/// <summary>
///     Semantic-phase extractor for <c>[GenerateDto]</c> targets.
/// </summary>
internal static class DtoExtractor
{
    internal static DtoExtractionResult Extract(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return new DtoExtractionResult(ExtractCore(context, cancellationToken), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Do not let an unusual, incomplete user symbol surface as Roslyn's CS8785
            // generator-failure diagnostic.  The source-output phase reports the stable CAERIUS008
            // diagnostic instead, without retaining an exception object in incremental state.
            return new DtoExtractionResult(null, exception.GetType().Name);
        }
    }

    private static DtoModel? ExtractCore(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol ||
            context.TargetNode is not TypeDeclarationSyntax)
            return null;

        var validation = TypeStructureValidator.Validate(typeSymbol);
        if (!validation.IsSealed || !validation.IsPartial || !validation.IsTopLevel || !validation.IsNonGeneric ||
            validation.PrimaryConstructorDeclaration is null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        var primaryCtorDecl = validation.PrimaryConstructorDeclaration!;
        var columns = ExtractColumns(primaryCtorDecl, validation.PrimaryConstructor!, cancellationToken);

        if (columns.Count == 0)
            return null;

        var typeKindKeyword = primaryCtorDecl.Kind() == SyntaxKind.ClassDeclaration ? "class" : "record";
        return new DtoModel(
            NamespaceHelper.GetNamespace(typeSymbol),
            typeSymbol.Name,
            GetAccessibilityKeyword(typeSymbol.DeclaredAccessibility),
            typeKindKeyword,
            columns,
            typeKindKeyword == "record" || HasReadableProperties(typeSymbol, columns, cancellationToken));
    }

    private static bool HasReadableProperties(
        INamedTypeSymbol typeSymbol,
        EquatableArray<ColumnModel> columns,
        CancellationToken cancellationToken)
    {
        foreach (var column in columns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!typeSymbol.GetMembers(column.Name).OfType<IPropertySymbol>()
                    .Any(property => property.GetMethod is not null))
                return false;
        }

        return true;
    }

    private static string GetAccessibilityKeyword(Accessibility accessibility)
    {
        return accessibility == Accessibility.Public ? "public" : "internal";
    }

    /// <summary>
    ///     Resolves the primary-constructor parameters as <see cref="ColumnModel" />s.
    /// </summary>
    private static EquatableArray<ColumnModel> ExtractColumns(
        TypeDeclarationSyntax primaryCtorDecl,
        IMethodSymbol primaryConstructor,
        CancellationToken cancellationToken)
    {
        var parameterList = primaryCtorDecl switch
        {
            RecordDeclarationSyntax r => r.ParameterList,
            ClassDeclarationSyntax c => c.ParameterList,
            _ => null
        };

        if (parameterList is null || parameterList.Parameters.Count == 0)
            return EquatableArray<ColumnModel>.Empty;

        var builder = ImmutableArray.CreateBuilder<ColumnModel>(primaryConstructor.Parameters.Length);
        for (var i = 0; i < primaryConstructor.Parameters.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parameter = primaryConstructor.Parameters[i];
            var column = ColumnExtractor.Extract(parameter, i);
            if (column is null)
                return EquatableArray<ColumnModel>.Empty;
            builder.Add(column);
        }

        return new EquatableArray<ColumnModel>(builder.ToImmutable());
    }
}

/// <summary>
///     The equatable result of extracting a DTO candidate.  A failure message is deliberately a compact
///     string rather than an exception so Roslyn can cache the incremental value safely.
/// </summary>
internal sealed record DtoExtractionResult(DtoModel? Model, string? FailureMessage);
