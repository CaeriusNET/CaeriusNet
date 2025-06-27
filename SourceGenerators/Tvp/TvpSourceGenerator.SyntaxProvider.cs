using System.Linq;
using System.Threading;
using CaeriusNet.Generator.Models;
using CaeriusNet.Generator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Metadata = CaeriusNet.Generator.Models.Metadata;

namespace CaeriusNet.Generator.Tvp;

public sealed partial class TvpSourceGenerator
{
    /// <summary>
    ///     Determines if a syntax node is a potential candidate for TVP generation.
    /// </summary>
    /// <param name="syntaxNode">The syntax node to evaluate.</param>
    /// <returns>True if the node could be a TVP candidate, false otherwise.</returns>
    private static bool IsTvpCandidate(SyntaxNode syntaxNode)
	{
		// We're looking for type declarations (class or record)
		if (syntaxNode is not TypeDeclarationSyntax typeDeclaration)
			return false;

		// Must be partial to allow code generation
		if (!typeDeclaration.Modifiers.Any(m => m.ValueText == "partial"))
			return false;

		// Must be sealed for performance and design reasons
		if (!typeDeclaration.Modifiers.Any(m => m.ValueText == "sealed"))
			return false;

		return true;
	}

    /// <summary>
    ///     Extracts metadata from a syntax node that has the GenerateTvp attribute.
    /// </summary>
    /// <param name="context">The generator attribute syntax context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata for TVP generation or null if extraction fails.</returns>
    private static Metadata? ExtractTvpMetadata(GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (context.TargetSymbol is not INamedTypeSymbol classSymbol)
			return null;

		if (context.TargetNode is not TypeDeclarationSyntax declarationSyntax)
			return null;

		// Get the namespace
		var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace
			? string.Empty
			: classSymbol.ContainingNamespace.ToDisplayString();

		// Create the base metadata
		var metadata = new Metadata(classSymbol, declarationSyntax, namespaceName);

		// Extract the TVP name from the attribute
		ExtractTvpNameFromAttribute(context, metadata);

		// Extract constructor parameters
		ExtractConstructorParameters(classSymbol, metadata);

		return metadata;
	}

    /// <summary>
    ///     Extracts the TVP name from the GenerateTvp attribute.
    /// </summary>
    /// <param name="context">The generator context.</param>
    /// <param name="metadata">The metadata to populate.</param>
    private static void ExtractTvpNameFromAttribute(GeneratorAttributeSyntaxContext context, Metadata metadata)
	{
		// Look for the attribute data
		var generateTvpAttribute = context.Attributes.FirstOrDefault(attr =>
			attr.AttributeClass?.Name == "GenerateTvpAttribute");

		if (generateTvpAttribute is null)
			return;

		// Check for positional argument (constructor parameter)
		if (generateTvpAttribute.ConstructorArguments.Length > 0)
		{
			var nameArg = generateTvpAttribute.ConstructorArguments[0];
			if (nameArg.Value is string tvpName && !string.IsNullOrWhiteSpace(tvpName))
			{
				metadata.CustomTvpName = tvpName;
				return;
			}
		}

		// Check for named argument (Name property)
		var namedArg = generateTvpAttribute.NamedArguments.FirstOrDefault(na => na.Key == "Name");
		if (namedArg.Value.Value is string namedTvpName && !string.IsNullOrWhiteSpace(namedTvpName))
			metadata.CustomTvpName = namedTvpName;
	}

    /// <summary>
    ///     Extracts constructor parameters from the type symbol.
    /// </summary>
    /// <param name="classSymbol">The class symbol to analyze.</param>
    /// <param name="metadata">The metadata to populate.</param>
    private static void ExtractConstructorParameters(INamedTypeSymbol classSymbol, Metadata metadata)
	{
		// Find the primary constructor or the constructor with the most parameters
		var constructor = classSymbol.Constructors
			.Where(c => !c.IsStatic)
			.OrderByDescending(c => c.Parameters.Length)
			.FirstOrDefault();

		if (constructor is null)
			return;

		for (var i = 0; i < constructor.Parameters.Length; i++)
		{
			var parameter = constructor.Parameters[i];
			var parameterType = parameter.Type;

			// Determine nullability
			var isNullable =
				TypeDetector.IsTypeNullable(parameterType, nullableAnnotation: parameter.NullableAnnotation);

			// Get SQL type mapping
			var sqlType = TypeDetector.GetSqlType(parameterType);
			var readerMethod = TypeDetector.GetReaderMethodForSqlType(sqlType);
			var requiresSpecialConversion = TypeDetector.RequiresSpecialConversion(parameterType.ToDisplayString());

			var parameterMetadata = new ParameterMetadata(
				parameter.Name,
				parameterType.ToDisplayString(),
				parameterType,
				isNullable,
				i,
				sqlType,
				readerMethod,
				requiresSpecialConversion
			);

			metadata.Parameters.Add(parameterMetadata);
		}
	}
}