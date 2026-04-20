namespace CaeriusNet.Generator.Tvp;

/// <summary>
///     Generates <see cref="ITvpMapper{T}" /> implementations for types decorated with
///     <see cref="GenerateTvpAttribute" />.
/// </summary>
/// <remarks>
///     <para>
///         Pipeline architecture mirrors <see cref="Dto.DtoSourceGenerator" />: attribute-based syntax filter
///         → value-equatable extraction → implementation-only emission.
///     </para>
///     <para>
///         Generated mappers stream rows via a single reused <c>SqlDataRecord</c> so TVP construction
///         allocates exactly one record + one schema array per type, no matter how many rows you pass.
///     </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class TvpSourceGenerator : IIncrementalGenerator
{
    internal const string AttributeMetadataName = "CaeriusNet.Attributes.Tvp.GenerateTvpAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var tvpCandidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeMetadataName,
                static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                static (ctx, ct) => TvpExtractor.Extract(ctx, ct));

        context.RegisterImplementationSourceOutput(tvpCandidates, static (spc, extraction) =>
        {
            foreach (var diag in extraction.Diagnostics)
                spc.ReportDiagnostic(diag.ToDiagnostic());

            if (extraction.Model is { Columns.Count: > 0 } model)
                spc.AddSource($"{model.TypeName}.g.cs", TvpEmitter.Emit(model));
        });
    }
}