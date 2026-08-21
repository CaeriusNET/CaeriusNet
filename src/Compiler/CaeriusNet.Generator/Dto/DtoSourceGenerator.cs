namespace CaeriusNet.Generator.Dto;

#pragma warning disable RS1007 // Generator diagnostics intentionally use invariant, secret-free text.
#pragma warning disable RS1015 // The package documentation is the public remediation guide.
#pragma warning disable RS1028 // This diagnostic does not require custom IDE filtering tags.

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
///                 <see cref="DtoExtractionResult" /> whose model is emitted only when the target shape is supported.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <see cref="IncrementalGeneratorInitializationContext.RegisterImplementationSourceOutput" />
///                 emits source and reports only the generator-containment diagnostic; ordinary user-facing
///                 shape and mapping diagnostics live in the companion analyzer.
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
    private const string AttributeMetadataName = "CaeriusNet.GenerateDtoAttribute";

    // A generator must never delegate a malformed user declaration to Roslyn's generic CS8785
    // failure diagnostic. All ordinary invalid mappings are filtered by DtoExtractor and reported
    // as CAERIUS005 by the companion analyzer; this descriptor is exclusively the last-resort
    // containment boundary for an unforeseen emitter defect.
#pragma warning disable RS1007 // Diagnostics deliberately use the invariant English strings shipped by the analyzer.
    private static readonly DiagnosticDescriptor EmissionFailure = new(
        "CAERIUS008",
        "DTO source emission failed",
        "CaeriusNet could not emit the DTO mapper for '{0}': {1}",
        "CaeriusNet.Analyzer",
        DiagnosticSeverity.Error,
        true,
        "Source emission was stopped before an internal generator exception could reach the compiler.",
        "https://github.com/CaeriusNET/CaeriusNet/blob/main/docs/diagnostics/CAERIUS008.md");
#pragma warning restore RS1007

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
            if (extraction.FailureMessage is not null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    EmissionFailure,
                    Location.None,
                    "the attributed DTO",
                    extraction.FailureMessage));
                return;
            }

            if (extraction.Model is not { Columns.Count: > 0 } model)
                return;

            try
            {
                spc.AddSource(HintNameBuilder.Build(model.Namespace, model.TypeName, "Dto"), DtoEmitter.Emit(model));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    EmissionFailure,
                    Location.None,
                    model.TypeName,
                    exception.Message));
            }
        });
    }
}

#pragma warning restore RS1028
#pragma warning restore RS1015
#pragma warning restore RS1007
