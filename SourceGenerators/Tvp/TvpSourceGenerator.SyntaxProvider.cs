namespace CaeriusNet.Generator.Tvp;

public sealed partial class TvpSourceGenerator
{
	/// <summary>
	///     Result of the TVP transform: the metadata payload (when extraction succeeded enough to be useful) and
	///     any diagnostics that should be reported to the user.
	/// </summary>
	internal sealed class TvpExtractionResult
	{
		internal Metadata? Metadata { get; init; }
		internal ImmutableArray<Diagnostic> Diagnostics { get; init; } = ImmutableArray<Diagnostic>.Empty;
		internal bool HasErrors { get; init; }
	}

	/// <summary>
	///     Fast syntactic filter: any class or record declaration is a candidate. The attribute name match performed
	///     by <see cref="SyntaxValueProvider.ForAttributeWithMetadataName" /> is the real filter; this predicate
	///     deliberately does NOT enforce <c>sealed</c>/<c>partial</c> so violations can be diagnosed downstream.
	/// </summary>
	private static bool IsTvpCandidate(SyntaxNode syntaxNode, CancellationToken cancellationToken)
	{
		_ = cancellationToken;
		return syntaxNode is ClassDeclarationSyntax or RecordDeclarationSyntax;
	}

	/// <summary>
	///     Extracts comprehensive metadata from a type decorated with <see cref="GenerateTvpAttribute" />, building
	///     diagnostics for every structural violation encountered.
	/// </summary>
	private static TvpExtractionResult ExtractTvpMetadata(GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (context.TargetSymbol is not INamedTypeSymbol classSymbol ||
		    context.TargetNode is not TypeDeclarationSyntax declarationSyntax)
			return new TvpExtractionResult();

		const string attributeDisplayName = "[GenerateTvp]";
		var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
		var location = declarationSyntax.Identifier.GetLocation();
		var hasError = false;

		var validation = TypeStructureValidator.Validate(classSymbol);

		if (!validation.IsSealed)
		{
			diagnostics.Add(Diagnostic.Create(
				DiagnosticDescriptors.MustBeSealed, location, classSymbol.Name, attributeDisplayName));
			hasError = true;
		}

		if (!validation.IsPartial)
		{
			diagnostics.Add(Diagnostic.Create(
				DiagnosticDescriptors.MustBePartial, location, classSymbol.Name, attributeDisplayName));
			hasError = true;
		}

		if (validation.PrimaryConstructorDeclaration is null)
		{
			diagnostics.Add(Diagnostic.Create(
				DiagnosticDescriptors.MustHavePrimaryConstructor, location, classSymbol.Name, attributeDisplayName));
			hasError = true;
		}

		// Determine the namespace (empty string for global namespace)
		var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace
			? string.Empty
			: classSymbol.ContainingNamespace.ToDisplayString();

		// Always create the metadata so callers can yield diagnostics even if generation is impossible.
		var metadata = new Metadata(classSymbol, validation.PrimaryConstructorDeclaration ?? declarationSyntax,
			namespaceName);

		// Extract TVP configuration from the attribute and validate non-empty TvpName
		var tvpNameDiag = ExtractTvpNameFromAttribute(context, metadata, location);
		if (tvpNameDiag is not null)
		{
			diagnostics.Add(tvpNameDiag);
			hasError = true;
		}

		// Only attempt parameter extraction when there is a primary constructor; without it, downstream
		// code generation cannot produce a useful mapper.
		if (validation.PrimaryConstructorDeclaration is not null)
			ExtractPrimaryConstructorParameters(validation.PrimaryConstructorDeclaration, classSymbol, metadata);

		return new TvpExtractionResult
		{
			Metadata = metadata,
			Diagnostics = diagnostics.ToImmutable(),
			HasErrors = hasError
		};
	}

	/// <summary>
	///     Extracts the TVP name and schema configuration from the <see cref="GenerateTvpAttribute" />.
	/// </summary>
	/// <returns>
	///     A diagnostic when <c>TvpName</c> is explicitly empty/whitespace; otherwise <see langword="null" />.
	/// </returns>
	private static Diagnostic? ExtractTvpNameFromAttribute(
		GeneratorAttributeSyntaxContext context,
		Metadata metadata,
		Location fallbackLocation)
	{
		var generateTvpAttribute = context.Attributes.FirstOrDefault(static attr =>
			attr.AttributeClass?.Name == "GenerateTvpAttribute");

		if (generateTvpAttribute is null)
			return null;

		// Schema (optional, defaults to "dbo")
		var schemaArg = generateTvpAttribute.NamedArguments.FirstOrDefault(static na => na.Key == "Schema");
		metadata.TvpSchema = schemaArg.Value.Value is string schema && !string.IsNullOrWhiteSpace(schema)
			? schema
			: "dbo";

		// TvpName (required by C# but possibly empty/whitespace at the call-site)
		var tvpNameArg = generateTvpAttribute.NamedArguments.FirstOrDefault(static na => na.Key == "TvpName");
		var tvpNameValue = tvpNameArg.Value.Value as string;

		if (!string.IsNullOrWhiteSpace(tvpNameValue))
		{
			metadata.TvpName = tvpNameValue;
			return null;
		}

		// Locate the attribute syntax for a more precise error location, if available.
		var attributeLocation = generateTvpAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
		                        ?? fallbackLocation;

		return Diagnostic.Create(
			DiagnosticDescriptors.TvpNameMustNotBeEmpty,
			attributeLocation,
			metadata.RecordName);
	}

	/// <summary>
	///     Extracts the primary constructor parameters of a TVP target. This intentionally mirrors the DTO contract
	///     (primary constructor only) rather than picking the longest constructor — keeps the mapping deterministic
	///     and consistent with the SQL column ordering expected by SqlMetaData.
	/// </summary>
	private static void ExtractPrimaryConstructorParameters(
		TypeDeclarationSyntax primaryCtorDeclaration,
		INamedTypeSymbol classSymbol,
		Metadata metadata)
	{
		var parameterList = primaryCtorDeclaration switch
		{
			RecordDeclarationSyntax r => r.ParameterList,
			ClassDeclarationSyntax c => c.ParameterList,
			_ => null
		};

		if (parameterList is null || parameterList.Parameters.Count == 0)
			return;

		// Prefer the symbol-based primary constructor (records expose it; classes with a primary ctor expose one
		// instance constructor matching the parameter count).
		var primaryConstructor = classSymbol.Constructors.FirstOrDefault(c =>
			!c.IsStatic && c.Parameters.Length == parameterList.Parameters.Count);

		if (primaryConstructor is null)
			return;

		var parameters = primaryConstructor.Parameters;
		for (var i = 0; i < parameters.Length; i++)
		{
			var parameter = parameters[i];
			var parameterType = parameter.Type;

			var isNullable = TypeDetector.IsTypeNullable(
				parameterType,
				nullableAnnotation: parameter.NullableAnnotation);

			var sqlType = TypeDetector.GetSqlType(parameterType);
			var readerMethod = TypeDetector.GetReaderMethodForSqlType(sqlType);
			var typeDisplayString = parameterType.ToDisplayString();
			var requiresSpecialConversion = TypeDetector.RequiresSpecialConversion(typeDisplayString);

			metadata.Parameters.Add(new ParameterMetadata(
				parameter.Name,
				typeDisplayString,
				parameterType,
				isNullable,
				i,
				sqlType,
				readerMethod,
				requiresSpecialConversion));
		}
	}
}
