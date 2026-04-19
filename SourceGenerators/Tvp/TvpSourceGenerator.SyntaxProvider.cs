namespace CaeriusNet.Generator.Tvp;

public sealed partial class TvpSourceGenerator
{
	/// <summary>
	///     Performs a fast syntactic check to determine if a node is a potential TVP generation candidate.
	/// </summary>
	/// <param name="syntaxNode">The syntax node to evaluate.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	///     <see langword="true" /> if the node is a sealed partial type declaration (class or record);
	///     otherwise, <see langword="false" />.
	/// </returns>
	/// <remarks>
	///     This predicate performs minimal work to quickly filter out non-candidates before semantic analysis.
	///     It checks for:
	///     <list type="bullet">
	///         <item>
	///             <description>Type declaration syntax (class or record)</description>
	///         </item>
	///         <item>
	///             <description>Sealed modifier (for performance and preventing inheritance issues)</description>
	///         </item>
	///         <item>
	///             <description>Partial modifier (required for code generation)</description>
	///         </item>
	///     </list>
	/// </remarks>
	private static bool IsTvpCandidate(SyntaxNode syntaxNode, CancellationToken cancellationToken)
    {
        if (syntaxNode is not TypeDeclarationSyntax typeDeclaration)
            return false;

        var modifiers = typeDeclaration.Modifiers;

        // Must be both partial and sealed
        var hasPartial = false;
        var hasSealed = false;

        foreach (var modifier in modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PartialKeyword))
                hasPartial = true;
            else if (modifier.IsKind(SyntaxKind.SealedKeyword))
                hasSealed = true;

            if (hasPartial && hasSealed)
                return true;
        }

        return false;
    }

	/// <summary>
	///     Extracts comprehensive metadata from a type decorated with <see cref="GenerateTvpAttribute" />.
	/// </summary>
	/// <param name="context">The generator attribute syntax context containing semantic information.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	///     A <see cref="Metadata" /> object containing all information needed for code generation,
	///     or <see langword="null" /> if the type is invalid or extraction fails.
	/// </returns>
	/// <remarks>
	///     This transform method performs semantic analysis to extract:
	///     <list type="bullet">
	///         <item>
	///             <description>Type and namespace information</description>
	///         </item>
	///         <item>
	///             <description>TVP name and schema from the attribute</description>
	///         </item>
	///         <item>
	///             <description>Constructor parameters with type mapping</description>
	///         </item>
	///     </list>
	/// </remarks>
	private static Metadata? ExtractTvpMetadata(GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol classSymbol)
            return null;

        if (context.TargetNode is not TypeDeclarationSyntax declarationSyntax)
            return null;

        // Determine the namespace (empty string for global namespace)
        var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : classSymbol.ContainingNamespace.ToDisplayString();

        // Create the metadata container
        var metadata = new Metadata(classSymbol, declarationSyntax, namespaceName);

        // Extract TVP configuration from the attribute
        ExtractTvpNameFromAttribute(context, metadata);

        // Extract and map constructor parameters
        ExtractConstructorParameters(classSymbol, metadata);

        return metadata;
    }

	/// <summary>
	///     Extracts the TVP name and schema configuration from the <see cref="GenerateTvpAttribute" />.
	/// </summary>
	/// <param name="context">The generator context containing attribute data.</param>
	/// <param name="metadata">The metadata object to populate with TVP configuration.</param>
	/// <remarks>
	///     This method reads the named arguments from the attribute:
	///     <list type="bullet">
	///         <item>
	///             <description><c>TvpName</c> (required): The name of the SQL Server TVP type</description>
	///         </item>
	///         <item>
	///             <description><c>Schema</c> (optional): The database schema, defaults to "dbo"</description>
	///         </item>
	///     </list>
	/// </remarks>
	private static void ExtractTvpNameFromAttribute(GeneratorAttributeSyntaxContext context, Metadata metadata)
    {
        // Locate the GenerateTvp attribute in the context
        var generateTvpAttribute = context.Attributes.FirstOrDefault(attr =>
            attr.AttributeClass?.Name == "GenerateTvpAttribute");

        if (generateTvpAttribute is null)
            return;

        // Extract TvpName (required property)
        var tvpNameArg =
            generateTvpAttribute.NamedArguments.FirstOrDefault(na => na.Key == "TvpName");

        if (tvpNameArg.Value.Value is string tvpName && !string.IsNullOrWhiteSpace(tvpName))
            metadata.TvpName = tvpName;

        // Extract Schema (optional, defaults to "dbo")
        var schemaArg =
            generateTvpAttribute.NamedArguments.FirstOrDefault(na => na.Key == "Schema");

        metadata.TvpSchema = schemaArg.Value.Value is string schema && !string.IsNullOrWhiteSpace(schema)
            ? schema
            : "dbo";
    }

	/// <summary>
	///     Extracts and maps constructor parameters to their SQL Server equivalents.
	/// </summary>
	/// <param name="classSymbol">The type symbol to analyze.</param>
	/// <param name="metadata">The metadata object to populate with parameter information.</param>
	/// <remarks>
	///     <para>
	///         This method identifies the primary constructor (or the constructor with the most parameters)
	///         and creates a <see cref="ParameterMetadata" /> entry for each parameter, including:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>Type mapping to SQL Server types</description>
	///         </item>
	///         <item>
	///             <description>Nullability analysis</description>
	///         </item>
	///         <item>
	///             <description>Appropriate SqlDataReader method selection</description>
	///         </item>
	///         <item>
	///             <description>Special conversion requirements (e.g., DateOnly, TimeOnly)</description>
	///         </item>
	///     </list>
	/// </remarks>
	private static void ExtractConstructorParameters(INamedTypeSymbol classSymbol, Metadata metadata)
    {
        // Find the primary constructor or the one with the most parameters
        var constructor = classSymbol.Constructors
            .Where(static c => !c.IsStatic)
            .OrderByDescending(static c => c.Parameters.Length)
            .FirstOrDefault();

        if (constructor is null)
            return;

        var parameters = constructor.Parameters;

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var parameterType = parameter.Type;

            // Analyze nullability using the type detector
            var isNullable = TypeDetector.IsTypeNullable(
                parameterType,
                nullableAnnotation: parameter.NullableAnnotation);

            // Map to SQL type and determine reader method
            var sqlType = TypeDetector.GetSqlType(parameterType);
            var readerMethod = TypeDetector.GetReaderMethodForSqlType(sqlType);
            var typeDisplayString = parameterType.ToDisplayString();
            var requiresSpecialConversion = TypeDetector.RequiresSpecialConversion(typeDisplayString);

            // Create parameter metadata
            var parameterMetadata = new ParameterMetadata(
                parameter.Name,
                typeDisplayString,
                parameterType,
                isNullable,
                i,
                sqlType,
                readerMethod,
                requiresSpecialConversion);

            metadata.Parameters.Add(parameterMetadata);
        }
    }
}