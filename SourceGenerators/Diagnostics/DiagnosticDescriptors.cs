namespace CaeriusNet.Generator.Diagnostics;

/// <summary>
///     Centralised set of <see cref="DiagnosticDescriptor" /> instances raised by CaeriusNet source generators.
/// </summary>
/// <remarks>
///     Each descriptor uses the <c>CAERIUS</c> prefix so they can be filtered, severity-overridden, or
///     suppressed via standard <c>.editorconfig</c> rules
///     (e.g. <c>dotnet_diagnostic.CAERIUS001.severity = warning</c>).
/// </remarks>
internal static class DiagnosticDescriptors
{
    private const string Category = "CaeriusNet.Generator";

    private const string HelpLinkBase =
        "https://github.com/CaeriusNET/CaeriusNet/blob/main/Documentations/diagnostics/";

    /// <summary>
    ///     CAERIUS001 — the decorated type is not declared <c>sealed</c>.
    /// </summary>
    /// <remarks>
    ///     Message format args: <c>{0}</c> = type name, <c>{1}</c> = attribute name (e.g. <c>[GenerateDto]</c>).
    /// </remarks>
    internal static readonly DiagnosticDescriptor MustBeSealed = new(
        "CAERIUS001",
        "Type must be sealed",
        "'{0}' is decorated with '{1}' but must be declared 'sealed' for source generation to proceed",
        Category,
        DiagnosticSeverity.Error,
        true,
        "CaeriusNet generators only target sealed types — this maximises devirtualisation, prevents inheritance hazards in the generated mapper, and matches the documented contract.",
        HelpLinkBase + "CAERIUS001.md");

    /// <summary>
    ///     CAERIUS002 — the decorated type is not declared <c>partial</c>.
    /// </summary>
    /// <remarks>
    ///     Message format args: <c>{0}</c> = type name, <c>{1}</c> = attribute name.
    /// </remarks>
    internal static readonly DiagnosticDescriptor MustBePartial = new(
        "CAERIUS002",
        "Type must be partial",
        "'{0}' is decorated with '{1}' but must be declared 'partial' so the generator can extend it",
        Category,
        DiagnosticSeverity.Error,
        true,
        "Generated mappers are emitted as partial-class extensions; the user-declared type must be partial to receive them.",
        HelpLinkBase + "CAERIUS002.md");

    /// <summary>
    ///     CAERIUS003 — the decorated type does not declare a primary constructor with at least one parameter.
    /// </summary>
    /// <remarks>
    ///     Message format args: <c>{0}</c> = type name, <c>{1}</c> = attribute name.
    /// </remarks>
    internal static readonly DiagnosticDescriptor MustHavePrimaryConstructor = new(
        "CAERIUS003",
        "Type must declare a primary constructor with parameters",
        "'{0}' is decorated with '{1}' but must declare a primary constructor with at least one parameter — these parameters drive the SQL column mapping",
        Category,
        DiagnosticSeverity.Error,
        true,
        "CaeriusNet uses primary-constructor parameter ordinals as the mapping contract between SQL columns and CLR properties. Without parameters there is nothing to map.",
        HelpLinkBase + "CAERIUS003.md");

    /// <summary>
    ///     CAERIUS004 — <c>[GenerateTvp]</c> was applied with an empty/whitespace <c>TvpName</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>TvpName</c> is C#-required, so a missing argument is already caught by the compiler. This diagnostic
    ///         fires only when the user explicitly assigns an empty or whitespace string.
    ///     </para>
    ///     <para>Message format args: <c>{0}</c> = type name.</para>
    /// </remarks>
    internal static readonly DiagnosticDescriptor TvpNameMustNotBeEmpty = new(
        "CAERIUS004",
        "[GenerateTvp] requires a non-empty TvpName",
        "'{0}' has '[GenerateTvp]' but its 'TvpName' is empty or whitespace — set it to the SQL Server table-type name",
        Category,
        DiagnosticSeverity.Error,
        true,
        "TvpName is propagated to the generated SqlMetaData and to the SqlParameter type name. An empty value would cause a runtime failure when the parameter is sent to the server.",
        HelpLinkBase + "CAERIUS004.md");

    /// <summary>
    ///     CAERIUS005 — a parameter type has no native SQL Server mapping and falls back to <c>sql_variant</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Surfaced as a warning (not an error) so existing code keeps building, but the user is alerted
    ///         that round-tripping the column will go through the boxed <c>sql_variant</c> path with all the
    ///         performance, indexing and type-safety penalties that implies.
    ///     </para>
    ///     <para>Message format args: <c>{0}</c> = parameter name, <c>{1}</c> = type name, <c>{2}</c> = owning type name.</para>
    /// </remarks>
    internal static readonly DiagnosticDescriptor UnsupportedSqlMapping = new(
        "CAERIUS005",
        "Parameter type has no native SQL Server mapping",
        "Parameter '{0}' of type '{1}' on '{2}' has no native SQL Server mapping and will be emitted as 'sql_variant'",
        Category,
        DiagnosticSeverity.Warning,
        true,
        "CaeriusNet maps CLR types to native SQL Server types where possible. Falling back to sql_variant works but disables indexing, computed-column participation, and several optimisations. Consider using a supported type or providing a custom mapping.",
        HelpLinkBase + "CAERIUS005.md");
}