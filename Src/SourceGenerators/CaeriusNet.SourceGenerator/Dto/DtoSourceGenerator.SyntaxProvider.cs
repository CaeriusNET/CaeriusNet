namespace CaeriusNet.SourceGenerator.Dto;

public sealed partial class DtoSourceGenerator
{
	/// <summary>
	///     Analyzes declarations to find types annotated with [GenerateDto] and extract their metadata.
	/// </summary>
	private static IEnumerable<Metadata?> GetDtoTypes(Compilation compilation,
		ImmutableArray<TypeDeclarationSyntax> declarations, CancellationToken cancellationToken)
	{
		if (declarations.IsDefaultOrEmpty) yield break;

		// Get the symbol for the GenerateDto attribute
		var generateDtoAttributeSymbol = compilation.GetTypeByMetadataName("CaeriusNet.Attributes.Dto.GenerateDtoAttribute");

		if (generateDtoAttributeSymbol is null)
			// GenerateDto attribute is not referenced in the compilation
			yield break;

		// Find and process all the DTO candidates
		foreach (var typeDeclaration in declarations){
			cancellationToken.ThrowIfCancellationRequested();

			// Get the semantic model for this syntax node
			var semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);

			// Get the type symbol for the class/record
			if (semanticModel.GetDeclaredSymbol(typeDeclaration) is not {} typeSymbol)
				continue;

			// Check if the type has the GenerateDto attribute
			if (!HasGenerateDtoAttribute(typeSymbol, generateDtoAttributeSymbol))
				continue;

			// Validate that the type is a valid candidate for generation
			if (!ValidateDtoType(typeDeclaration))
				continue;

			// Get the namespace
			string namespaceName = GetNamespace(typeSymbol);

			// Create the DTO metadata
			var dtoMetadata = new Metadata(typeSymbol, typeDeclaration, namespaceName);

			// Extract parameter information from primary constructor
			if (!ExtractConstructorParameters(dtoMetadata, semanticModel))
				continue;

			yield return dtoMetadata;
		}
	}

	/// <summary>
	///     Checks if a type has the GenerateDto attribute.
	/// </summary>
	private static bool HasGenerateDtoAttribute(INamedTypeSymbol typeSymbol, INamedTypeSymbol attributeSymbol)
	{
		return typeSymbol.GetAttributes()
			.Any(a => a.AttributeClass?.Equals(attributeSymbol, SymbolEqualityComparer.Default) == true);
	}

	/// <summary>
	///     Validates that the DTO type meets the requirements for generation.
	/// </summary>
	private static bool ValidateDtoType(TypeDeclarationSyntax typeDeclaration)
	{
		// Check if the type is partial
		if (!typeDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
			return false;

		// Check if the type is sealed
		if (!typeDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword)))
			return false;

		return typeDeclaration switch
		{
			// If it's a class, ensure it has a primary constructor
			ClassDeclarationSyntax classDeclaration => classDeclaration.ParameterList != null,
			// If it's a record, it always has a primary constructor (if parameters are present)
			RecordDeclarationSyntax recordDeclaration => recordDeclaration.ParameterList != null,
			_ => false
		};
	}

	/// <summary>
	///     Gets the fully qualified namespace for a type.
	/// </summary>
	private static string GetNamespace(INamedTypeSymbol typeSymbol)
	{
		// Handle global namespace
		return string.IsNullOrEmpty(typeSymbol.ContainingNamespace?.ToDisplayString())
			? "global"
			: typeSymbol.ContainingNamespace!.ToDisplayString();
	}

	/// <summary>
	///     Extracts and validates constructor parameters from a DTO type.
	/// </summary>
	private static bool ExtractConstructorParameters(Metadata metadata, SemanticModel semanticModel)
	{
		// Get the primary constructor parameters

		var parameterList = metadata.DeclarationSyntax switch
		{
			RecordDeclarationSyntax recordDeclaration => recordDeclaration.ParameterList,
			ClassDeclarationSyntax classDeclaration => classDeclaration.ParameterList,
			_ => null
		};

		if (parameterList == null || parameterList.Parameters.Count == 0)
			return false;

		// Process each parameter
		for (int i = 0; i < parameterList.Parameters.Count; i++){
			var parameterSyntax = parameterList.Parameters[i];
			var parameterSymbol = semanticModel.GetDeclaredSymbol(parameterSyntax);

			if (parameterSymbol == null)
				continue;

			// Get the type information
			string typeName = parameterSymbol.Type.ToDisplayString();
			bool isNullable = TypeDetector.IsTypeNullable(parameterSymbol.Type, parameterSyntax.Type,
			parameterSymbol.NullableAnnotation);

			// Check if the type is an enum
			bool isEnum = TypeDetector.IsEnumType(parameterSymbol.Type);

			// Get SQL type and reader method
			string sqlType = TypeDetector.GetSqlType(parameterSymbol.Type);
			string readerMethod = TypeDetector.GetReaderMethodForSqlType(sqlType);
			bool requiresSpecialConversion = TypeDetector.RequiresSpecialConversion(typeName) || isEnum;

			// Create the parameter metadata
			var parameterMetadata = new ParameterMetadata(
			parameterSymbol.Name,
			typeName,
			parameterSymbol.Type,
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