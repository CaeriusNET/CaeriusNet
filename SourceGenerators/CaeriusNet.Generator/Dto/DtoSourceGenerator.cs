namespace CaeriusNet.Generator.Dto;

[Generator(LanguageNames.CSharp)]
public sealed partial class DtoSourceGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Register the attribute that will be available to users in their code
		context.RegisterPostInitializationOutput(ctx =>
		{
			ctx.AddSource(SourceGenerateDtoAttribute.GlobalName, SourceGenerateDtoAttribute.Source);
		});

		// Create a syntax provider to find all classes/records with [GenerateDto] attribute
		var syntaxProvider = context.SyntaxProvider
			.CreateSyntaxProvider(Predicate, Transform)
			.Where(record => record is not null)
			.Collect();

		// Check if the generator is enabled in the project configuration
		var enabled = context.AnalyzerConfigOptionsProvider.IsEnabled("GenerateDto");

		// Combine the syntax provider with the enabled flag
		var provider = syntaxProvider.Combine(enabled);

		// Register the output for the source generation
		context.RegisterSourceOutput(provider, (spc, pair) => Generate(spc, pair.Left!, pair.Right));
	}
}