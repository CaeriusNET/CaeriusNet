namespace CaeriusNet.Analyzer.Diagnostics;

/// <summary>
///     Centralized set of <see cref="DiagnosticDescriptor" /> instances reported by CaeriusNet analyzers.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "CaeriusNet.Generator";

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
}
