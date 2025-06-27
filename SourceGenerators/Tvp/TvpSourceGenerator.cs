using Microsoft.CodeAnalysis;

namespace CaeriusNet.Generator.Tvp;

/// <summary>
///     Source generator that creates ITvpMapper implementations for types marked with the [GenerateTvp] attribute.
///     This generator enables automatic conversion of C# objects into SQL Server Table-Valued Parameters (TVPs).
/// </summary>
[Generator]
public sealed partial class TvpSourceGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Create the pipeline for detecting and generating TVP mappers
		var tvpCandidates = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				"CaeriusNet.Attributes.Tvp.GenerateTvpAttribute",
				static (syntaxNode, cancellationToken) => IsTvpCandidate(syntaxNode),
				static (context, cancellationToken) => ExtractTvpMetadata(context, cancellationToken))
			.Where(static metadata => metadata is not null);

		// Register the code generation
		context.RegisterSourceOutput(tvpCandidates, static (context, tvpMetadata) =>
		{
			if (tvpMetadata is not null) GenerateTvpMapper(context, tvpMetadata);
		});
	}
}