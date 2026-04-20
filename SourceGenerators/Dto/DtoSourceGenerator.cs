namespace CaeriusNet.Generator.Dto;

/// <summary>
///     Generates <see cref="ISpMapper{T}" /> implementations for types decorated with
///     <see cref="GenerateDtoAttribute" />.
/// </summary>
/// <remarks>
///     <para>
///         The pipeline is intentionally kept minimal so that Roslyn's incremental engine can cache
///         everything that is not affected by user edits:
///     </para>
///     <list type="number">
///         <item>
///             <description>
///                 <see cref="SyntaxValueProvider.ForAttributeWithMetadataName{T}" /> filters candidate
///                 syntax nodes by attribute name without ever forcing a semantic model walk on unrelated code.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <see cref="DtoExtractor.Extract" /> projects a candidate onto a value-equatable
///                 <see cref="ExtractionResult{TModel}" /> — no Roslyn types leak through the pipeline.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <see cref="IncrementalGeneratorInitializationContext.RegisterImplementationSourceOutput" />
///                 emits diagnostics and source: the work re-runs only when the model changes.
///             </description>
///         </item>
///     </list>
///     <para>
///         Generated mappers ship with an explicit <c>// &lt;auto-generated /&gt;</c> banner so analyzers
///         skip them and consumers know to never edit them by hand.
///     </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class DtoSourceGenerator : IIncrementalGenerator
{
    /// <summary>
    ///     The fully qualified metadata name of the marker attribute. Kept in one place so refactoring
    ///     in the consuming library cannot silently break the generator.
    /// </summary>
    internal const string AttributeMetadataName = "CaeriusNet.Attributes.Dto.GenerateDtoAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var dtoCandidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeMetadataName,
                static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                static (ctx, ct) => DtoExtractor.Extract(ctx, ct));

        context.RegisterImplementationSourceOutput(dtoCandidates, static (spc, extraction) =>
        {
            foreach (var diag in extraction.Diagnostics)
                spc.ReportDiagnostic(diag.ToDiagnostic());

            if (extraction.Model is { Columns.Count: > 0 } model)
                spc.AddSource($"{model.TypeName}.g.cs", DtoEmitter.Emit(model));
        });
    }
}