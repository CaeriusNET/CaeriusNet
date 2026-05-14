namespace CaeriusNet.Analyzer.Diagnostics;

/// <summary>
///     Centralized set of <see cref="DiagnosticDescriptor" /> instances reported by CaeriusNet analyzers.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "CaeriusNet.Analyzer";

    private const string HelpLinkBase =
        "https://github.com/CaeriusNET/CaeriusNet/blob/main/Documentations/diagnostics/";

    internal static readonly DiagnosticDescriptor MustBeSealed = new(
        "CAERIUS001",
        "Type must be sealed",
        "'{0}' is decorated with '{1}' but must be declared 'sealed' for source generation to proceed",
        Category,
        DiagnosticSeverity.Error,
        true,
        "CaeriusNet generators only target sealed types to avoid inheritance hazards in generated mappers.",
        HelpLinkBase + "CAERIUS001.md");

    internal static readonly DiagnosticDescriptor MustBePartial = new(
        "CAERIUS002",
        "Type must be partial",
        "'{0}' is decorated with '{1}' but must be declared 'partial' so the generator can extend it",
        Category,
        DiagnosticSeverity.Error,
        true,
        "Generated mappers are emitted as partial-class extensions; the user-declared type must be partial.",
        HelpLinkBase + "CAERIUS002.md");

    internal static readonly DiagnosticDescriptor MustHavePrimaryConstructor = new(
        "CAERIUS003",
        "Type must declare a primary constructor with parameters",
        "'{0}' is decorated with '{1}' but must declare a primary constructor with at least one parameter -- these parameters drive the SQL column mapping",
        Category,
        DiagnosticSeverity.Error,
        true,
        "CaeriusNet uses primary-constructor parameters as the mapping contract between SQL columns and CLR values.",
        HelpLinkBase + "CAERIUS003.md");

    internal static readonly DiagnosticDescriptor TvpNameMustNotBeEmpty = new(
        "CAERIUS004",
        "[GenerateTvp] requires a non-empty TvpName",
        "'{0}' has '[GenerateTvp]' but its 'TvpName' is empty or whitespace -- set it to the SQL Server table-type name",
        Category,
        DiagnosticSeverity.Error,
        true,
        "TvpName is propagated to generated SQL metadata and must be non-empty.",
        HelpLinkBase + "CAERIUS004.md");

    internal static readonly DiagnosticDescriptor UnsupportedSqlMapping = new(
        "CAERIUS005",
        "Parameter type has no native SQL Server mapping",
        "Parameter '{0}' of type '{1}' on '{2}' has no native SQL Server mapping and will be emitted as 'sql_variant'",
        Category,
        DiagnosticSeverity.Warning,
        true,
        "Falling back to sql_variant works but carries performance, indexing, and type-safety penalties.",
        HelpLinkBase + "CAERIUS005.md");

    internal static readonly DiagnosticDescriptor UnsupportedGeneratorTarget = new(
        "CAERIUS006",
        "Generator target shape is not supported",
        "'{0}' is decorated with '{1}' but generator targets must be non-generic top-level types",
        Category,
        DiagnosticSeverity.Error,
        true,
        "CaeriusNet generators emit companion partial declarations and currently support only non-generic top-level types.",
        HelpLinkBase + "CAERIUS006.md");

    internal static readonly DiagnosticDescriptor ContractManifestMissing = CreateCompilationEnd(
        "CAERIUS200",
        "Contract manifest is missing",
        "The CaeriusNet contract manifest '{0}' was not found",
        DiagnosticSeverity.Error,
        "Generated SQL Server contracts require a caerius.contracts.json manifest supplied as an AdditionalFiles item.",
        HelpLinkBase + "CAERIUS200.md");

    internal static readonly DiagnosticDescriptor ContractProcedureMissing = CreateCompilationEnd(
        "CAERIUS201",
        "Procedure is missing from contract manifest",
        "Procedure '{0}' is missing from the CaeriusNet contract manifest",
        DiagnosticSeverity.Error,
        "The generated procedure descriptor must come from the read-only SQL Server contract manifest.",
        HelpLinkBase + "CAERIUS201.md");

    internal static readonly DiagnosticDescriptor ContractTableTypeMissing = CreateCompilationEnd(
        "CAERIUS202",
        "TVP is missing from contract manifest",
        "TVP '{0}' is referenced but is missing from the CaeriusNet contract manifest",
        DiagnosticSeverity.Error,
        "Every generated structured parameter must reference a table type present in the contract manifest.",
        HelpLinkBase + "CAERIUS202.md");

    internal static readonly DiagnosticDescriptor ContractUnsupportedTableTypeSqlType = CreateCompilationEnd(
        "CAERIUS203",
        "TVP SQL type is not supported",
        "TVP column '{0}' uses unsupported SQL type '{1}'",
        DiagnosticSeverity.Error,
        "Generated TVP metadata must preserve SQL Server facets and can only emit supported SqlMetaData mappings.",
        HelpLinkBase + "CAERIUS203.md");

    internal static readonly DiagnosticDescriptor ContractUndeterminedResultSet = CreateCompilationEnd(
        "CAERIUS204",
        "First result set cannot be determined",
        "The first result set of procedure '{0}' cannot be determined by SQL Server",
        DiagnosticSeverity.Error,
        "Automatic DTO generation requires SQL Server to describe the first result set from metadata.",
        HelpLinkBase + "CAERIUS204.md");

    internal static readonly DiagnosticDescriptor ContractNoResultSet = CreateCompilationEnd(
        "CAERIUS205",
        "Procedure has no result set",
        "Procedure '{0}' has no result set; no result DTO will be generated",
        DiagnosticSeverity.Warning,
        "Procedures without a first result set can still get descriptors and parameter builders.",
        HelpLinkBase + "CAERIUS205.md");

    internal static readonly DiagnosticDescriptor ContractOutputParameterUnsupported = CreateCompilationEnd(
        "CAERIUS206",
        "Output parameter is not supported",
        "Procedure '{0}' has output parameter '{1}', which is not supported by generated contracts",
        DiagnosticSeverity.Error,
        "Generated contracts support input parameters only.",
        HelpLinkBase + "CAERIUS206.md");

    internal static readonly DiagnosticDescriptor ContractUnsupportedSqlType = CreateCompilationEnd(
        "CAERIUS207",
        "SQL type is not mappable",
        "SQL type '{0}' cannot be mapped to generated C# code",
        DiagnosticSeverity.Error,
        "Unsupported SQL Server types must be handled explicitly instead of falling back to runtime reflection or dynamic mapping.",
        HelpLinkBase + "CAERIUS207.md");

    internal static readonly DiagnosticDescriptor ContractNullableResultColumn = CreateCompilationEnd(
        "CAERIUS208",
        "Nullable result column",
        "Result column '{0}' is nullable and will be emitted as a nullable CLR type",
        DiagnosticSeverity.Warning,
        "Nullable SQL Server result columns are represented with nullable CLR types unless a stricter project policy rejects them.",
        HelpLinkBase + "CAERIUS208.md");

    internal static readonly DiagnosticDescriptor ContractHashMismatch = CreateCompilationEnd(
        "CAERIUS209",
        "Contract hash mismatch",
        "Contract hash mismatch for '{0}'",
        DiagnosticSeverity.Error,
        "Verify mode compares read-only SQL Server metadata with the manifest hash to catch schema drift.",
        HelpLinkBase + "CAERIUS209.md");

    internal static readonly DiagnosticDescriptor ContractPossibleIncompatibility = CreateCompilationEnd(
        "CAERIUS210",
        "Procedure may be incompatible with generated contracts",
        "Procedure '{0}' may be incompatible with generated contracts: {1}",
        DiagnosticSeverity.Warning,
        "SQL Server metadata discovery can be conservative for dynamic SQL, temporary tables, ambiguous projections, or unsupported constructs.",
        HelpLinkBase + "CAERIUS210.md");

    private static DiagnosticDescriptor CreateCompilationEnd(
        string id,
        string title,
        string messageFormat,
        DiagnosticSeverity severity,
        string description,
        string helpLinkUri)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            messageFormat,
            Category,
            severity,
            true,
            description,
            helpLinkUri,
            WellKnownDiagnosticTags.CompilationEnd);
    }
}
