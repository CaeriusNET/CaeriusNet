namespace CaeriusNet.Analyzer;

/// <summary>
///     Reports user-facing diagnostics for types annotated with CaeriusNet generator attributes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeneratorUsageAnalyzer : DiagnosticAnalyzer
{
    private const string DtoAttributeMetadataName = "CaeriusNet.Attributes.Dto.GenerateDtoAttribute";
    private const string TvpAttributeMetadataName = "CaeriusNet.Attributes.Tvp.GenerateTvpAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        DiagnosticDescriptors.MustBeSealed,
        DiagnosticDescriptors.MustBePartial,
        DiagnosticDescriptors.MustHavePrimaryConstructor,
        DiagnosticDescriptors.TvpNameMustNotBeEmpty,
        DiagnosticDescriptors.UnsupportedSqlMapping,
        DiagnosticDescriptors.UnsupportedGeneratorTarget
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            var dtoAttribute = startContext.Compilation.GetTypeByMetadataName(DtoAttributeMetadataName);
            var tvpAttribute = startContext.Compilation.GetTypeByMetadataName(TvpAttributeMetadataName);
            if (dtoAttribute is null && tvpAttribute is null)
                return;

            startContext.RegisterSyntaxNodeAction(
                context => AnalyzeTypeDeclaration(context, dtoAttribute, tvpAttribute),
                SyntaxKind.ClassDeclaration,
                SyntaxKind.RecordDeclaration);
        });
    }

    private static void AnalyzeTypeDeclaration(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? dtoAttribute,
        INamedTypeSymbol? tvpAttribute)
    {
        if (context.Node is not TypeDeclarationSyntax declaration ||
            declaration.AttributeLists.Count == 0 ||
            context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } typeSymbol)
            return;

        if (dtoAttribute is not null && TryGetDirectAttribute(typeSymbol, dtoAttribute) is { } dtoAttributeData)
            AnalyzeDto(context, typeSymbol, dtoAttributeData);

        if (tvpAttribute is not null && TryGetDirectAttribute(typeSymbol, tvpAttribute) is { } tvpAttributeData)
            AnalyzeTvp(context, typeSymbol, tvpAttributeData);
    }

    private static void AnalyzeDto(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attributeData)
    {
        AnalyzeCommon(
            context,
            typeSymbol,
            attributeData,
            "[GenerateDto]",
            static (_, _, _, _, _) => false);
    }

    private static void AnalyzeTvp(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attributeData)
    {
        AnalyzeCommon(
            context,
            typeSymbol,
            attributeData,
            "[GenerateTvp]",
            AnalyzeTvpAttribute);
    }

    private static void AnalyzeCommon(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attributeData,
        string attributeDisplayName,
        Func<SyntaxNodeAnalysisContext, INamedTypeSymbol, AttributeData, TypeStructureValidator.ValidationResult, string
                , bool>
            extraValidation)
    {
        var validation = TypeStructureValidator.Validate(typeSymbol);
        var hasFatal = ReportStructureDiagnostics(context, typeSymbol, validation, attributeDisplayName);
        hasFatal |= extraValidation(context, typeSymbol, attributeData, validation, attributeDisplayName);

        if (hasFatal || validation.PrimaryConstructorDeclaration is null)
            return;

        ReportUnsupportedSqlMappings(context, typeSymbol, validation.PrimaryConstructorDeclaration);
    }

    private static bool AnalyzeTvpAttribute(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attributeData,
        TypeStructureValidator.ValidationResult _,
        string _1)
    {
        if (!TryGetNamedArgumentSyntax(attributeData, context.CancellationToken, "TvpName", out var argumentSyntax))
            return false;

        var constantValue =
            context.SemanticModel.GetConstantValue(argumentSyntax.Expression, context.CancellationToken);
        if (constantValue.Value is not string tvpName || !string.IsNullOrWhiteSpace(tvpName))
            return false;

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.TvpNameMustNotBeEmpty,
                argumentSyntax.Expression.GetLocation(),
                typeSymbol.Name));

        return true;
    }

    private static bool ReportStructureDiagnostics(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        TypeStructureValidator.ValidationResult validation,
        string attributeDisplayName)
    {
        var hasFatal = false;
        var location = TypeStructureValidator.GetIdentifierLocation(typeSymbol);

        if (!validation.IsSealed)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.MustBeSealed,
                    location,
                    typeSymbol.Name,
                    attributeDisplayName));
            hasFatal = true;
        }

        if (!validation.IsPartial)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.MustBePartial,
                    location,
                    typeSymbol.Name,
                    attributeDisplayName));
            hasFatal = true;
        }

        if (!validation.IsTopLevel || !validation.IsNonGeneric)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedGeneratorTarget,
                    location,
                    typeSymbol.Name,
                    attributeDisplayName));
            hasFatal = true;
        }

        if (validation.PrimaryConstructorDeclaration is not null) return hasFatal;
        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.MustHavePrimaryConstructor,
                location,
                typeSymbol.Name,
                attributeDisplayName));
        hasFatal = true;

        return hasFatal;
    }

    private static void ReportUnsupportedSqlMappings(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        TypeDeclarationSyntax primaryConstructorDeclaration)
    {
        var parameterList = primaryConstructorDeclaration switch
        {
            RecordDeclarationSyntax recordDeclaration => recordDeclaration.ParameterList,
            ClassDeclarationSyntax classDeclaration => classDeclaration.ParameterList,
            _ => null
        };

        if (parameterList is null || parameterList.Parameters.Count == 0)
            return;

        var primaryConstructor = typeSymbol.Constructors.FirstOrDefault(static constructor => !constructor.IsStatic
            && constructor.Parameters.Length > 0
            && constructor.DeclaringSyntaxReferences.Length > 0);
        if (primaryConstructor is null || primaryConstructor.Parameters.Length != parameterList.Parameters.Count)
            return;

        for (var i = 0; i < primaryConstructor.Parameters.Length; i++)
        {
            var parameter = primaryConstructor.Parameters[i];
            var parameterType = parameter.Type;
            if (parameterType.TypeKind == TypeKind.Error ||
                !string.Equals(SqlTypeDetector.GetSqlType(parameterType), "sql_variant", StringComparison.Ordinal))
                continue;

            var parameterSyntax = parameterList.Parameters[i];
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedSqlMapping,
                    parameterSyntax.GetLocation(),
                    parameter.Name,
                    parameterType.ToDisplayString(),
                    typeSymbol.Name));
        }
    }

    private static AttributeData? TryGetDirectAttribute(INamedTypeSymbol typeSymbol, INamedTypeSymbol attributeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))
                return attribute;

        return null;
    }

    private static bool TryGetNamedArgumentSyntax(
        AttributeData attributeData,
        CancellationToken cancellationToken,
        string argumentName,
        out AttributeArgumentSyntax argumentSyntax)
    {
        if (attributeData.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is AttributeSyntax
            {
                ArgumentList: not null
            } attributeSyntax)
            foreach (var argument in attributeSyntax.ArgumentList.Arguments)
            {
                if (argument.NameEquals?.Name.Identifier.ValueText != argumentName) continue;
                argumentSyntax = argument;
                return true;
            }

        argumentSyntax = null!;
        return false;
    }
}
