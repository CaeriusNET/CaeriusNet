using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace CaeriusNet.Generator.Dto;

/// <summary>
///     Source generator that creates ISpMapper implementations for DTOs marked with the [GenerateDto] attribute.
///     This main class coordinates the generation workflow.
/// </summary>
[Generator]
public sealed partial class DtoSourceGenerator : IIncrementalGenerator
{
	/// <summary>
	///     Initializes the source generator.
	/// </summary>
	/// <param name="context">The generation initialization context.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Filter for syntax nodes that might be DTO records or classes
		var classDeclarations = context.SyntaxProvider.CreateSyntaxProvider(
			predicate: static (s, _) => IsTargetForGeneration(s),
			transform: static (ctx, _) => GetTypeDeclarationForGeneration(ctx))
			.Where(static m => m is not null);

		// Combine with compilation
		IncrementalValueProvider<(Compilation Compilation, ImmutableArray<TypeDeclarationSyntax> Declarations)>
			compilationAndTypes =
				context.CompilationProvider.Combine(classDeclarations.Collect());

		// Register the source generator
		context.RegisterSourceOutput(compilationAndTypes,
		action: static (spc, source) => Execute(source.Compilation, source.Declarations, spc));
	}

	/// <summary>
	///     Initial filter to quickly exclude syntax nodes that definitely aren't DTO candidates.
	/// </summary>
	private static bool IsTargetForGeneration(SyntaxNode node)
	{
		// We're looking for class or record declarations with attributes
		return node is TypeDeclarationSyntax { AttributeLists.Count: > 0 }
			and (ClassDeclarationSyntax or RecordDeclarationSyntax);
	}

	/// <summary>
	///     Extracts type declarations that are viable candidates for DTO generation.
	/// </summary>
	private static TypeDeclarationSyntax GetTypeDeclarationForGeneration(GeneratorSyntaxContext context)
	{
		// Get the type declaration
		var typeDeclaration = (TypeDeclarationSyntax)context.Node;

		// Return it for further processing in the main execution
		return typeDeclaration;
	}

	/// <summary>
	///     Main source generation execution method.
	/// </summary>
	private static void Execute(Compilation compilation, ImmutableArray<TypeDeclarationSyntax> declarations,
		SourceProductionContext context)
	{
		if (declarations.IsDefaultOrEmpty)
			// Nothing to generate, exit early
			return;

		// Find all DTO candidates and generate for them
		foreach (var dtoMetadata in GetDtoTypes(compilation, declarations, context.CancellationToken)){
			if (dtoMetadata is null) continue;

			// Generate the source code
			string source = GenerateMapperSource(dtoMetadata);

			// Add the generated source to the compilation
			context.AddSource($"{dtoMetadata.RecordName}.g.cs", source);
		}
	}
}