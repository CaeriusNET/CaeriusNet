using CaeriusNet.Analyzer.Roslyn;

namespace CaeriusNet.Analyzer;

/// <summary>
///     Reports user-facing diagnostics for types annotated with CaeriusNet generator attributes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeneratorUsageAnalyzer : DiagnosticAnalyzer
{
    private const string DtoAttributeMetadataName = "CaeriusNet.GenerateDtoAttribute";
    private const string TvpAttributeMetadataName = "CaeriusNet.GenerateTvpAttribute";

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

            startContext.RegisterSymbolAction(
                context => AnalyzeNamedType(context, dtoAttribute, tvpAttribute),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        INamedTypeSymbol? dtoAttribute,
        INamedTypeSymbol? tvpAttribute)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol)
            return;

        if (dtoAttribute is not null && TryGetDirectAttribute(typeSymbol, dtoAttribute) is { } dtoAttributeData)
            AnalyzeDto(context, typeSymbol, dtoAttributeData);

        if (tvpAttribute is not null && TryGetDirectAttribute(typeSymbol, tvpAttribute) is { } tvpAttributeData)
            AnalyzeTvp(context, typeSymbol, tvpAttributeData);
    }

    private static void AnalyzeDto(
        SymbolAnalysisContext context,
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
        SymbolAnalysisContext context,
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
        SymbolAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attributeData,
        string attributeDisplayName,
        Func<SymbolAnalysisContext, INamedTypeSymbol, AttributeData, TypeStructureValidator.ValidationResult, string
                , bool>
            extraValidation)
    {
        var validation = TypeStructureValidator.Validate(typeSymbol);
        var hasFatal = ReportStructureDiagnostics(context, typeSymbol, validation, attributeDisplayName);
        hasFatal |= extraValidation(context, typeSymbol, attributeData, validation, attributeDisplayName);

        if (hasFatal || validation.PrimaryConstructorDeclaration is null)
            return;

        ReportUnsupportedSqlMappings(
            context,
            typeSymbol,
            validation.PrimaryConstructorDeclaration,
            validation.PrimaryConstructor!);
    }

    private static bool AnalyzeTvpAttribute(
        SymbolAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attributeData,
        TypeStructureValidator.ValidationResult _,
        string _1)
    {
        if (!TryGetNamedArgument(attributeData, "TvpName", out var tvpNameConstant) ||
            tvpNameConstant.Value is not string tvpName ||
            !string.IsNullOrWhiteSpace(tvpName))
            return false;

        var location = TryGetNamedArgumentSyntax(attributeData, "TvpName", out var argumentSyntax,
            context.CancellationToken)
            ? argumentSyntax.Expression.GetLocation()
            : TypeStructureValidator.GetIdentifierLocation(typeSymbol);

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.TvpNameMustNotBeEmpty,
                location,
                typeSymbol.Name));

        return true;
    }

    private static bool ReportStructureDiagnostics(
        SymbolAnalysisContext context,
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
        SymbolAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        TypeDeclarationSyntax primaryConstructorDeclaration,
        IMethodSymbol primaryConstructor)
    {
        var parameterList = primaryConstructorDeclaration switch
        {
            RecordDeclarationSyntax recordDeclaration => recordDeclaration.ParameterList,
            ClassDeclarationSyntax classDeclaration => classDeclaration.ParameterList,
            _ => null
        };

        if (parameterList is null || parameterList.Parameters.Count == 0)
            return;

        if (primaryConstructor.Parameters.Length != parameterList.Parameters.Count)
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

    private static bool TryGetNamedArgument(
        AttributeData attributeData,
        string argumentName,
        out TypedConstant value)
    {
        foreach (var argument in attributeData.NamedArguments)
        {
            if (!string.Equals(argument.Key, argumentName, StringComparison.Ordinal))
                continue;

            value = argument.Value;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetNamedArgumentSyntax(
        AttributeData attributeData,
        string argumentName,
        out AttributeArgumentSyntax argumentSyntax,
        CancellationToken cancellationToken)
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
