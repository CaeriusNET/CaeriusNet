namespace CaeriusNet.Generator.Dto;

public sealed partial class DtoSourceGenerator
{
	/// <summary>
	///     Analyzes type declarations to find and extract metadata from types decorated with
	///     <see cref="GenerateDtoAttribute" />.
	/// </summary>
	/// <param name="compilation">The current compilation containing type information.</param>
	/// <param name="declarations">The collection of type declarations to analyze.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	///     An enumerable sequence of <see cref="Metadata" /> objects for valid DTO types,
	///     or <see langword="null" /> for invalid or incomplete candidates.
	/// </returns>
	/// <remarks>
	///     <para>
	///         This method performs semantic analysis on candidate types to:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>Verify the presence of <see cref="GenerateDtoAttribute" /></description>
	///         </item>
	///         <item>
	///             <description>Validate type structure (sealed, partial, primary constructor)</description>
	///         </item>
	///         <item>
	///             <description>Extract namespace and type information</description>
	///         </item>
	///         <item>
	///             <description>Map constructor parameters to SQL types</description>
	///         </item>
	///     </list>
	/// </remarks>
	private static IEnumerable<Metadata?> GetDtoTypes(
		Compilation compilation,
		ImmutableArray<TypeDeclarationSyntax> declarations,
		CancellationToken cancellationToken)
	{
		if (declarations.IsDefaultOrEmpty)
			yield break;

		// Resolve the GenerateDto attribute symbol
		var generateDtoAttributeSymbol =
			compilation.GetTypeByMetadataName("CaeriusNet.Attributes.Dto.GenerateDtoAttribute");

		if (generateDtoAttributeSymbol is null)
			yield break;

		// Process each candidate type declaration
		foreach (var typeDeclaration in declarations){
			cancellationToken.ThrowIfCancellationRequested();

			// Get semantic model for this syntax tree
			var semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);

			// Resolve the type symbol
			if (semanticModel.GetDeclaredSymbol(typeDeclaration) is not {} typeSymbol)
				continue;

			// Verify attribute presence
			if (!HasGenerateDtoAttribute(typeSymbol, generateDtoAttributeSymbol))
				continue;

			// Validate type structure
			if (!ValidateDtoType(typeDeclaration))
				continue;

			// Extract namespace information
			string namespaceName = GetNamespace(typeSymbol);

			// Create metadata container
			var dtoMetadata = new Metadata(typeSymbol, typeDeclaration, namespaceName);

			// Extract and map constructor parameters
			if (!ExtractConstructorParameters(dtoMetadata, semanticModel))
				continue;

			yield return dtoMetadata;
		}
	}

	/// <summary>
	///     Determines whether a type has the <see cref="GenerateDtoAttribute" /> applied.
	/// </summary>
	/// <param name="typeSymbol">The type symbol to check.</param>
	/// <param name="attributeSymbol">The resolved GenerateDto attribute symbol.</param>
	/// <returns>
	///     <see langword="true" /> if the type has the GenerateDto attribute; otherwise, <see langword="false" />.
	/// </returns>
	private static bool HasGenerateDtoAttribute(INamedTypeSymbol typeSymbol, INamedTypeSymbol attributeSymbol)
	{
		return typeSymbol.GetAttributes()
			.Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeSymbol));
	}

	/// <summary>
	///     Validates that a DTO type meets all structural requirements for code generation.
	/// </summary>
	/// <param name="typeDeclaration">The type declaration to validate.</param>
	/// <returns>
	///     <see langword="true" /> if the type is valid for generation; otherwise, <see langword="false" />.
	/// </returns>
	/// <remarks>
	///     Valid DTO types must:
	///     <list type="bullet">
	///         <item>
	///             <description>Be declared as <c>partial</c> (to allow code generation)</description>
	///         </item>
	///         <item>
	///             <description>Be declared as <c>sealed</c> (for performance and design safety)</description>
	///         </item>
	///         <item>
	///             <description>Have a primary constructor with at least one parameter</description>
	///         </item>
	///     </list>
	/// </remarks>
	private static bool ValidateDtoType(TypeDeclarationSyntax typeDeclaration)
	{
		var modifiers = typeDeclaration.Modifiers;

		// Verify required modifiers: partial and sealed
		bool hasPartial = false;
		bool hasSealed = false;

		foreach (var modifier in modifiers){
			if (modifier.IsKind(SyntaxKind.PartialKeyword))
				hasPartial = true;
			else if (modifier.IsKind(SyntaxKind.SealedKeyword))
				hasSealed = true;

			if (hasPartial && hasSealed)
				break;
		}

		if (!hasPartial || !hasSealed)
			return false;

		// Verify primary constructor presence
		return typeDeclaration switch
		{
			ClassDeclarationSyntax classDeclaration => classDeclaration.ParameterList is not null,
			RecordDeclarationSyntax recordDeclaration => recordDeclaration.ParameterList is not null,
			_ => false
		};
	}

	/// <summary>
	///     Extracts the fully qualified namespace for a type.
	/// </summary>
	/// <param name="typeSymbol">The type symbol to analyze.</param>
	/// <returns>
	///     The fully qualified namespace name, or "global" if the type is in the global namespace.
	/// </returns>
	private static string GetNamespace(INamedTypeSymbol typeSymbol)
	{
		return typeSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns
			? ns.ToDisplayString()
			: "global";
	}

	/// <summary>
	///     Extracts and maps constructor parameters from the DTO's primary constructor.
	/// </summary>
	/// <param name="metadata">The metadata object to populate with parameter information.</param>
	/// <param name="semanticModel">The semantic model for resolving parameter symbols.</param>
	/// <returns>
	///     <see langword="true" /> if at least one parameter was successfully extracted;
	///     otherwise, <see langword="false" />.
	/// </returns>
	/// <remarks>
	///     <para>
	///         This method analyzes each primary constructor parameter and creates a <see cref="ParameterMetadata" />
	///         entry containing:
	///     </para>
	///     <list type="bullet">
	///         <item>
	///             <description>Type name and symbol information</description>
	///         </item>
	///         <item>
	///             <description>Nullability analysis (considering nullable reference types and Nullable&lt;T&gt;)</description>
	///         </item>
	///         <item>
	///             <description>SQL Server type mapping</description>
	///         </item>
	///         <item>
	///             <description>Appropriate SqlDataReader method selection</description>
	///         </item>
	///         <item>
	///             <description>Special conversion requirements (enums, DateOnly, TimeOnly, byte[])</description>
	///         </item>
	///         <item>
	///             <description>Ordinal position for efficient column access</description>
	///         </item>
	///     </list>
	/// </remarks>
	private static bool ExtractConstructorParameters(Metadata metadata, SemanticModel semanticModel)
	{
		// Resolve the primary constructor parameter list
		var parameterList = metadata.DeclarationSyntax switch
		{
			RecordDeclarationSyntax recordDeclaration => recordDeclaration.ParameterList,
			ClassDeclarationSyntax classDeclaration => classDeclaration.ParameterList,
			_ => null
		};

		if (parameterList is null || parameterList.Parameters.Count == 0)
			return false;

		var parameters = parameterList.Parameters;

		// Process each parameter in order (ordinal position matters for SqlDataReader)
		for (int i = 0; i < parameters.Count; i++){
			var parameterSyntax = parameters[i];
			var parameterSymbol = semanticModel.GetDeclaredSymbol(parameterSyntax);

			if (parameterSymbol is null)
				continue;

			var parameterType = parameterSymbol.Type;
			string typeName = parameterType.ToDisplayString();

			// Analyze nullability considering all C# nullability features
			bool isNullable = TypeDetector.IsTypeNullable(
			parameterType,
			parameterSyntax.Type,
			parameterSymbol.NullableAnnotation);

			// Determine if enum (requires cast in generated code)
			bool isEnum = TypeDetector.IsEnumType(parameterType);

			// Map to SQL Server type and determine appropriate reader method
			string sqlType = TypeDetector.GetSqlType(parameterType);
			string readerMethod = TypeDetector.GetReaderMethodForSqlType(sqlType);
			bool requiresSpecialConversion = TypeDetector.RequiresSpecialConversion(typeName) || isEnum;

			// Create and add parameter metadata
			var parameterMetadata = new ParameterMetadata(
			parameterSymbol.Name,
			typeName,
			parameterType,
			isNullable,
			i,// Ordinal position
			sqlType,
			readerMethod,
			requiresSpecialConversion);

			metadata.Parameters.Add(parameterMetadata);
		}

		return metadata.Parameters.Count > 0;
	}
}