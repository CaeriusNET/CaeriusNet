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
    private const string AttributeMetadataName = "CaeriusNet.GenerateTvpAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var tvpCandidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeMetadataName,
                static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                static (ctx, ct) => TvpExtractor.Extract(ctx, ct));

        context.RegisterImplementationSourceOutput(tvpCandidates, static (spc, model) =>
        {
            foreach (var issue in model.Issues)
                spc.ReportDiagnostic(Diagnostic.Create(
                    TvpGeneratorDiagnostics.GetDescriptor(issue.DiagnosticId),
                    Location.None,
                    issue.ParameterName,
                    issue.Message));

            if (model.Model is { Columns.Count: > 0 } tvpModel)
                spc.AddSource(HintNameBuilder.Build(tvpModel.Namespace, tvpModel.TypeName, "Tvp"),
                    TvpEmitter.Emit(tvpModel));
        });
    }
}
