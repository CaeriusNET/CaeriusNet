namespace CaeriusNet.Generator.Dto;

/// <summary>
///     Generates <see cref="ISpMapper{T}" /> implementations for types decorated with <see cref="GenerateDtoAttribute" />.
/// </summary>
/// <remarks>
///     <para>
///         This incremental source generator automatically creates implementations of the <see cref="ISpMapper{T}" />
///         interface,
///         which maps data from <see cref="Microsoft.Data.SqlClient.SqlDataReader" /> to strongly-typed C# DTOs.
///     </para>
///     <para>
///         The generator processes sealed partial records/classes with primary constructors and generates the
///         <c>MapFromDataReader</c> method, which efficiently reads columns by ordinal position and handles nullability,
///         type conversions, and special cases like <see cref="DateOnly" /> and <see cref="TimeOnly" />.
///     </para>
///     <para>
///         Performance characteristics:
///         <list type="bullet">
///             <item>
///                 <description>Incremental generation: Only regenerates when source changes</description>
///             </item>
///             <item>
///                 <description>Efficient syntax filtering before semantic analysis</description>
///             </item>
///             <item>
///                 <description>Ordinal-based column access for optimal SqlDataReader performance</description>
///             </item>
///             <item>
///                 <description>Compile-time null handling for maximum runtime efficiency</description>
///             </item>
///         </list>
///     </para>
/// </remarks>
[Generator]
public sealed partial class DtoSourceGenerator : IIncrementalGenerator
{
	/// <summary>
	///     Initializes the incremental source generator pipeline.
	/// </summary>
	/// <param name="context">The initialization context providing access to the compilation and syntax providers.</param>
	/// <remarks>
	///     This method sets up a multi-stage pipeline:
	///     <list type="number">
	///         <item>
	///             <description>Syntax filtering: Quickly identifies potential DTO candidates</description>
	///         </item>
	///         <item>
	///             <description>Semantic analysis: Validates candidates and extracts metadata</description>
	///         </item>
	///         <item>
	///             <description>Code generation: Produces ISpMapper implementation source code</description>
	///         </item>
	///     </list>
	/// </remarks>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Filter for syntax nodes that might be DTO records or classes
		var classDeclarations = context.SyntaxProvider
			.CreateSyntaxProvider(
			predicate: static (s, _) => IsTargetForGeneration(s),
			transform: static (ctx, _) => GetTypeDeclarationForGeneration(ctx))
			.Where(static m => m is not null);

		// Combine with compilation for semantic analysis
		IncrementalValueProvider<(Compilation Compilation, ImmutableArray<TypeDeclarationSyntax> Declarations)>
			compilationAndTypes = context.CompilationProvider.Combine(classDeclarations.Collect());

		// Register the code generation action
		context.RegisterSourceOutput(compilationAndTypes,
		action: static (spc, source) => Execute(source.Compilation, source.Declarations, spc));
	}

	/// <summary>
	///     Performs a fast syntactic filter to identify potential DTO generation candidates.
	/// </summary>
	/// <param name="node">The syntax node to evaluate.</param>
	/// <returns>
	///     <see langword="true" /> if the node is a class or record declaration with attributes;
	///     otherwise, <see langword="false" />.
	/// </returns>
	/// <remarks>
	///     This predicate performs minimal work to quickly exclude non-candidates before semantic analysis.
	///     It looks for type declarations (class or record) that have at least one attribute.
	/// </remarks>
	private static bool IsTargetForGeneration(SyntaxNode node)
	{
		// Looking for class or record declarations with attributes
		return node is TypeDeclarationSyntax { AttributeLists.Count: > 0 }
			and (ClassDeclarationSyntax or RecordDeclarationSyntax);
	}

	/// <summary>
	///     Extracts the type declaration from the generator syntax context.
	/// </summary>
	/// <param name="context">The generator syntax context containing the syntax node.</param>
	/// <returns>The type declaration syntax for further processing.</returns>
	/// <remarks>
	///     This transform simply casts the node to <see cref="TypeDeclarationSyntax" />
	///     for collection and later semantic analysis.
	/// </remarks>
	private static TypeDeclarationSyntax GetTypeDeclarationForGeneration(GeneratorSyntaxContext context)
	{
		return (TypeDeclarationSyntax)context.Node;
	}

	/// <summary>
	///     Executes the main source generation process for all DTO candidates.
	/// </summary>
	/// <param name="compilation">The current compilation containing type information.</param>
	/// <param name="declarations">The collection of type declarations to process.</param>
	/// <param name="context">The source production context for adding generated files.</param>
	/// <remarks>
	///     <para>
	///         This method orchestrates the complete generation workflow:
	///     </para>
	///     <list type="number">
	///         <item>
	///             <description>Validates and extracts metadata from candidate types</description>
	///         </item>
	///         <item>
	///             <description>Generates ISpMapper implementation source code</description>
	///         </item>
	///         <item>
	///             <description>Adds generated files to the compilation with "{TypeName}.g.cs" naming</description>
	///         </item>
	///     </list>
	/// </remarks>
	private static void Execute(
		Compilation compilation,
		ImmutableArray<TypeDeclarationSyntax> declarations,
		SourceProductionContext context)
	{
		if (declarations.IsDefaultOrEmpty)
			return;

		// Process each DTO candidate and generate mapper implementations
		foreach (var dtoMetadata in GetDtoTypes(compilation, declarations, context.CancellationToken)){
			if (dtoMetadata is null)
				continue;

			// Generate and add the source code
			string source = GenerateMapperSource(dtoMetadata);
			context.AddSource($"{dtoMetadata.RecordName}.g.cs", source);
		}
	}
}