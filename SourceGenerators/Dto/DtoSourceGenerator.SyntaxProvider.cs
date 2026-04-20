namespace CaeriusNet.Generator.Dto;

public sealed partial class DtoSourceGenerator
{
	/// <summary>
	///     Analyzes type declarations to find and extract metadata from types decorated with
	///     <see cref="GenerateDtoAttribute" />, while reporting structured diagnostics for invalid candidates.
	/// </summary>
	/// <param name="compilation">The current compilation containing type information.</param>
	/// <param name="declarations">The collection of type declarations to analyze.</param>
	/// <param name="context">The source production context used to surface diagnostics.</param>
	/// <returns>
	///     An enumerable sequence of <see cref="Metadata" /> objects for valid DTO types.
	/// </returns>
	private static IEnumerable<Metadata> GetDtoTypes(
		Compilation compilation,
		ImmutableArray<TypeDeclarationSyntax> declarations,
		SourceProductionContext context)
	{
		if (declarations.IsDefaultOrEmpty)
			yield break;

		// Resolve the GenerateDto attribute symbol
		var generateDtoAttributeSymbol =
			compilation.GetTypeByMetadataName("CaeriusNet.Attributes.Dto.GenerateDtoAttribute");

		if (generateDtoAttributeSymbol is null)
			yield break;

		const string attributeDisplayName = "[GenerateDto]";

		// Process each candidate type declaration
		foreach (var typeDeclaration in declarations)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			// Get semantic model for this syntax tree
			var semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);

			// Resolve the type symbol
			if (semanticModel.GetDeclaredSymbol(typeDeclaration) is not { } typeSymbol)
				continue;

			// Verify attribute presence at the symbol level
			if (!HasGenerateDtoAttribute(typeSymbol, generateDtoAttributeSymbol))
				continue;

			// For partial types the same symbol shows up once per declaration; only handle the declaration
			// that actually carries the attribute so diagnostics are not emitted multiple times.
			if (!DeclarationHasAttribute(typeDeclaration, generateDtoAttributeSymbol, semanticModel))
				continue;

			// Validate symbol-level structural requirements (handles split partial declarations correctly)
			var validation = TypeStructureValidator.Validate(typeSymbol);
			var location = typeDeclaration.Identifier.GetLocation();
			var hasError = false;

			if (!validation.IsSealed)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					DiagnosticDescriptors.MustBeSealed, location, typeSymbol.Name, attributeDisplayName));
				hasError = true;
			}

			if (!validation.IsPartial)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					DiagnosticDescriptors.MustBePartial, location, typeSymbol.Name, attributeDisplayName));
				hasError = true;
			}

			if (validation.PrimaryConstructorDeclaration is null)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					DiagnosticDescriptors.MustHavePrimaryConstructor, location, typeSymbol.Name,
					attributeDisplayName));
				hasError = true;
			}

			if (hasError)
				continue;

			// Extract namespace information
			var namespaceName = GetNamespace(typeSymbol);

			// Use the declaration that actually carries the primary constructor — handles split partials
			var declarationForExtraction = validation.PrimaryConstructorDeclaration!;
			var dtoMetadata = new Metadata(typeSymbol, declarationForExtraction, namespaceName);

			// Extract and map constructor parameters
			if (!ExtractConstructorParameters(dtoMetadata, semanticModel))
				continue;

			yield return dtoMetadata;
		}
	}

	/// <summary>
	///     Determines whether a type has the <see cref="GenerateDtoAttribute" /> applied.
	/// </summary>
	private static bool HasGenerateDtoAttribute(INamedTypeSymbol typeSymbol, INamedTypeSymbol attributeSymbol)
	{
		return typeSymbol.GetAttributes()
			.Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeSymbol));
	}

	/// <summary>
	///     Returns <see langword="true" /> when this specific declaration syntax carries the
	///     <see cref="GenerateDtoAttribute" />; used to avoid emitting duplicate diagnostics for partial types.
	/// </summary>
	private static bool DeclarationHasAttribute(
		TypeDeclarationSyntax declaration,
		INamedTypeSymbol attributeSymbol,
		SemanticModel semanticModel)
	{
		foreach (var attributeList in declaration.AttributeLists)
			foreach (var attribute in attributeList.Attributes)
			{
				var resolved = semanticModel.GetSymbolInfo(attribute).Symbol?.ContainingType;
				if (SymbolEqualityComparer.Default.Equals(resolved, attributeSymbol))
					return true;
			}

		return false;
	}

	/// <summary>
	///     Extracts the fully qualified namespace for a type.
	/// </summary>
	/// <returns>The fully qualified namespace name, or "global" if the type is in the global namespace.</returns>
	private static string GetNamespace(INamedTypeSymbol typeSymbol)
	{
		return typeSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns
			? ns.ToDisplayString()
			: "global";
	}

	/// <summary>
	///     Extracts and maps constructor parameters from the DTO's primary constructor.
	/// </summary>
	/// <returns>
	///     <see langword="true" /> if at least one parameter was successfully extracted; otherwise, <see langword="false" />.
	/// </returns>
	private static bool ExtractConstructorParameters(Metadata metadata, SemanticModel semanticModel)
	{
		var parameterList = metadata.DeclarationSyntax switch
		{
			RecordDeclarationSyntax recordDeclaration => recordDeclaration.ParameterList,
			ClassDeclarationSyntax classDeclaration => classDeclaration.ParameterList,
			_ => null
		};

		if (parameterList is null || parameterList.Parameters.Count == 0)
			return false;

		// Cross-tree primary constructors require resolving the parameter symbol on the model that owns the syntax.
		var parameterSemanticModel = parameterList.SyntaxTree == metadata.DeclarationSyntax.SyntaxTree
			? semanticModel
			: semanticModel.Compilation.GetSemanticModel(parameterList.SyntaxTree);

		var parameters = parameterList.Parameters;

		for (var i = 0; i < parameters.Count; i++)
		{
			var parameterSyntax = parameters[i];
			var parameterSymbol = parameterSemanticModel.GetDeclaredSymbol(parameterSyntax);

			if (parameterSymbol is null)
				continue;

			var parameterType = parameterSymbol.Type;
			var typeName = parameterType.ToDisplayString();

			var isNullable = TypeDetector.IsTypeNullable(
				parameterType,
				parameterSyntax.Type,
				parameterSymbol.NullableAnnotation);

			var isEnum = TypeDetector.IsEnumType(parameterType);
			var sqlType = TypeDetector.GetSqlType(parameterType);
			var readerMethod = TypeDetector.GetReaderMethodForSqlType(sqlType);
			var requiresSpecialConversion = TypeDetector.RequiresSpecialConversion(typeName) || isEnum;

			var parameterMetadata = new ParameterMetadata(
				parameterSymbol.Name,
				typeName,
				parameterType,
				isNullable,
				i,
				sqlType,
				readerMethod,
				requiresSpecialConversion);

			metadata.Parameters.Add(parameterMetadata);
		}

		return metadata.Parameters.Count > 0;
	}
}
