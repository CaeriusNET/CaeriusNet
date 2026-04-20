namespace CaeriusNet.Generator.Tvp;

/// <summary>
///     Generates <see cref="ITvpMapper{T}" /> implementations for types decorated with <see cref="GenerateTvpAttribute" />
///     .
/// </summary>
/// <remarks>
///     <para>
///         This incremental source generator automatically creates implementations of the <see cref="ITvpMapper{T}" />
///         interface,
///         which converts C# objects into SQL Server Table-Valued Parameters (TVPs) via
///         <see cref="System.Data.SqlTypes.SqlDataRecord" /> streaming — no <c>DataTable</c> allocation required.
///     </para>
///     <para>
///         The generator processes sealed partial records/classes and generates the <c>MapAsSqlDataRecords</c> method,
///         which streams rows using a single reused <c>SqlDataRecord</c> instance with a static <c>_tvpMetaData</c>
///         array, enabling zero-allocation TVP construction.
///     </para>
///     <para>
///         Performance characteristics:
///         <list type="bullet">
///             <item>
///                 <description>Incremental generation: Only regenerates when source changes</description>
///             </item>
///             <item>
///                 <description>Zero-allocation attribute detection using ForAttributeWithMetadataName</description>
///             </item>
///             <item>
///                 <description>Efficient metadata extraction with cancellation support</description>
///             </item>
///         </list>
///     </para>
/// </remarks>
[Generator]
public sealed partial class TvpSourceGenerator : IIncrementalGenerator
{
	/// <summary>
	///     Initializes the incremental source generator pipeline.
	/// </summary>
	/// <param name="context">The initialization context providing access to the compilation and syntax providers.</param>
	/// <remarks>
	///     This method sets up a two-stage pipeline:
	///     <list type="number">
	///         <item>
	///             <description>
	///                 Detection: Identifies types with <see cref="GenerateTvpAttribute" /> that meet structural
	///                 requirements
	///             </description>
	///         </item>
	///         <item>
	///             <description>Generation: Produces ITvpMapper implementation source code for each valid candidate</description>
	///         </item>
	///     </list>
	/// </remarks>
	public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Create the pipeline for detecting and generating TVP mappers
        var tvpCandidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "CaeriusNet.Attributes.Tvp.GenerateTvpAttribute",
                static (syntaxNode, cancellationToken) => IsTvpCandidate(syntaxNode, cancellationToken),
                static (context, cancellationToken) => ExtractTvpMetadata(context, cancellationToken));

        // Register the code generation action: emit any diagnostics first, then generate when valid.
        context.RegisterSourceOutput(tvpCandidates, static (context, extraction) =>
        {
            foreach (var diagnostic in extraction.Diagnostics)
                context.ReportDiagnostic(diagnostic);

            if (!extraction.HasErrors && extraction.Metadata is not null && extraction.Metadata.Parameters.Count > 0)
                GenerateTvpMapper(context, extraction.Metadata);
        });
    }
}