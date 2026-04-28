using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CaeriusNet.Telemetry;

/// <summary>
///     Centralized <see cref="System.Diagnostics.ActivitySource" /> and
///     <see cref="System.Diagnostics.Metrics.Meter" /> for the CaeriusNet library.
/// </summary>
/// <remarks>
///     <para>
///         The library only emits OpenTelemetry-compatible signals via the BCL
///         (<see cref="System.Diagnostics.ActivitySource" /> / <see cref="System.Diagnostics.Metrics.Meter" />).
///         It does not depend on any OpenTelemetry SDK package — consumers (typically an
///         Aspire <c>ServiceDefaults</c> project) opt-in by calling:
///     </para>
///     <code>
/// tracing.AddSource(CaeriusDiagnostics.SourceName);
/// metrics.AddMeter(CaeriusDiagnostics.SourceName);
/// </code>
///     <para>
///         Spans are created with <see cref="ActivityKind.Client" /> and follow the OpenTelemetry
///         Semantic Conventions for database calls (<c>db.system</c>, <c>db.name</c>,
///         <c>db.operation</c>, <c>db.statement</c>) plus library-specific attributes prefixed by
///         <c>caerius.*</c>.
///     </para>
/// </remarks>
public static class CaeriusDiagnostics
{
    /// <summary>
    ///     Public name of the <see cref="System.Diagnostics.ActivitySource" /> and
    ///     <see cref="System.Diagnostics.Metrics.Meter" /> emitted by CaeriusNet.
    /// </summary>
    public const string SourceName = "CaeriusNet";

    /// <summary>
    ///     Version of the library reported on the <see cref="ActivitySource" /> and <see cref="Meter" />.
    /// </summary>
    public static readonly string SourceVersion =
        typeof(CaeriusDiagnostics).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    /// <summary>
    ///     <see cref="System.Diagnostics.ActivitySource" /> used by all CaeriusNet stored-procedure
    ///     and transaction commands.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(SourceName, SourceVersion);

    /// <summary>
    ///     <see cref="System.Diagnostics.Metrics.Meter" /> used by CaeriusNet to publish duration,
    ///     execution count, error count and cache lookup metrics.
    /// </summary>
    public static readonly Meter Meter = new(SourceName, SourceVersion);

    /// <summary>
    ///     Histogram of stored-procedure execution duration, in milliseconds.
    /// </summary>
    public static readonly Histogram<double> SpDuration = Meter.CreateHistogram<double>(
        "caerius.sp.duration",
        "ms",
        "Duration of CaeriusNet stored procedure executions.");

    /// <summary>
    ///     Counter of stored-procedure executions that completed successfully.
    ///     Failed calls are counted in <see cref="SpErrors" /> instead of here.
    ///     Cache-hit short-circuits are not counted — no SQL was executed.
    /// </summary>
    public static readonly Counter<long> SpExecutions = Meter.CreateCounter<long>(
        "caerius.sp.executions",
        description: "Number of CaeriusNet stored procedure executions that completed successfully.");

    /// <summary>
    ///     Counter of stored-procedure executions that ended in a SQL error.
    /// </summary>
    public static readonly Counter<long> SpErrors = Meter.CreateCounter<long>(
        "caerius.sp.errors",
        description: "Number of CaeriusNet stored procedure executions that failed with a SQL error.");

    /// <summary>
    ///     Counter of cache lookups performed by CaeriusNet. Tagged with <c>caerius.cache.tier</c>
    ///     (<c>InMemory</c>, <c>Frozen</c>, <c>Redis</c>) and <c>caerius.cache.hit</c> (<c>true</c>/<c>false</c>).
    /// </summary>
    public static readonly Counter<long> CacheLookups = Meter.CreateCounter<long>(
        "caerius.cache.lookups",
        description: "Number of CaeriusNet cache lookups, tagged by tier and hit/miss.");

    /// <summary>
    ///     Current telemetry options applied to every span and metric emitted by CaeriusNet.
    ///     Set once at startup via <c>CaeriusNetBuilder.WithTelemetryOptions(…)</c>;
    ///     defaults to <see cref="CaeriusTelemetryOptions" /> with all options at their safe defaults.
    /// </summary>
    public static CaeriusTelemetryOptions TelemetryOptions { get; internal set; } = new();

    /// <summary>
    ///     Well-known attribute names emitted on spans and metric tags.
    /// </summary>
    public static class AttributeNames
    {
        // OpenTelemetry semantic conventions for databases.
        public const string DbSystem = "db.system";
        public const string DbName = "db.name";
        public const string DbOperation = "db.operation";
        public const string DbStatement = "db.statement";

        // Library-specific tags.
        public const string SpSchema = "caerius.sp.schema";
        public const string SpName = "caerius.sp.name";
        public const string SpParameters = "caerius.sp.parameters";
        public const string SpCommand = "caerius.sp.command";
        public const string TvpUsed = "caerius.tvp.used";
        public const string TvpTypeName = "caerius.tvp.type_name";
        public const string ResultSetMulti = "caerius.resultset.multi";
        public const string ResultSetExpectedCount = "caerius.resultset.expected_count";
        public const string RowsReturned = "caerius.rows_returned";
        public const string RowsAffected = "caerius.rows_affected";
        public const string CacheTier = "caerius.cache.tier";
        public const string CacheHit = "caerius.cache.hit";
        public const string Transactional = "caerius.tx";
        public const string TxIsolationLevel = "caerius.tx.isolation_level";
        public const string TxOutcome = "caerius.tx.outcome";
    }

    /// <summary>
    ///     Well-known constant tag values.
    /// </summary>
    public static class AttributeValues
    {
        public const string DbSystemMsSql = "mssql";
    }
}
