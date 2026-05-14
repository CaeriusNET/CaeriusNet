using System.Diagnostics;
using System.Text;

namespace CaeriusNet.Telemetry;

/// <summary>
///     Internal helpers that start <see cref="Activity" /> instances and emit metrics for the
///     CaeriusNet command pipeline. Designed to be zero-cost when no listener is registered.
/// </summary>
internal static class CaeriusActivityExtensions
{
    /// <summary>
    ///     Starts a new <see cref="ActivityKind.Client" /> span describing a stored-procedure execution.
    /// </summary>
    /// <param name="spParameters">Parameters of the stored procedure being executed.</param>
    /// <param name="operation">
    ///     Logical operation name of the calling command (e.g. <c>"FirstQueryAsync"</c>,
    ///     <c>"QueryMultipleImmutableArrayAsync"</c>, <c>"ExecuteNonQueryAsync"</c>).
    /// </param>
    /// <param name="transactional">
    ///     <see langword="true" /> when the call runs inside an open <c>ICaeriusNetTransaction</c>.
    /// </param>
    /// <param name="expectedResultSetCount">
    ///     Number of result sets expected by the caller. Defaults to <c>1</c>. Multi-result-set
    ///     command overloads pass the tuple arity (2, 3, 4, 5).
    /// </param>
    /// <returns>
    ///     The started <see cref="Activity" />, or <see langword="null" /> when no listener is subscribed
    ///     to <see cref="CaeriusDiagnostics.ActivitySource" /> (no allocation in that case).
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Activity? StartStoredProcedureActivity(
        StoredProcedureParameters spParameters,
        string operation,
        bool transactional = false,
        int expectedResultSetCount = 1)
    {
        var source = CaeriusDiagnostics.ActivitySource;
        if (!source.HasListeners())
            return null;

        var activity = source.StartActivity(
            string.Concat("SP ", spParameters.SchemaName, ".", spParameters.ProcedureName),
            ActivityKind.Client);

        if (activity is null)
            return null;

        activity.SetTag(CaeriusDiagnostics.AttributeNames.DbSystem,
            CaeriusDiagnostics.AttributeValues.DbSystemMsSql);
        activity.SetTag(CaeriusDiagnostics.AttributeNames.DbOperation, operation);
        activity.SetTag(CaeriusDiagnostics.AttributeNames.DbStatement,
            string.Concat(spParameters.SchemaName, ".", spParameters.ProcedureName));

        activity.SetTag(CaeriusDiagnostics.AttributeNames.SpSchema, spParameters.SchemaName);
        activity.SetTag(CaeriusDiagnostics.AttributeNames.SpName, spParameters.ProcedureName);
        activity.SetTag(CaeriusDiagnostics.AttributeNames.SpCommand, operation);

        var paramsSpan = spParameters.GetParametersSpan();
        if (paramsSpan.Length > 0)
            activity.SetTag(CaeriusDiagnostics.AttributeNames.SpParameters,
                CaeriusDiagnostics.TelemetryOptions.CaptureParameterValues
                    ? JoinParameterNamesAndValues(paramsSpan)
                    : JoinParameterNames(paramsSpan));

        // Detect TVP usage by scanning the parameter array — no need to store it separately.
        var tvpNames = ExtractTvpTypeNames(paramsSpan);
        var tvpUsed = tvpNames.Count > 0;
        activity.SetTag(CaeriusDiagnostics.AttributeNames.TvpUsed, tvpUsed);
        if (tvpUsed)
            activity.SetTag(CaeriusDiagnostics.AttributeNames.TvpTypeName,
                tvpNames.Count == 1
                    ? tvpNames[0]
                    : string.Join(",", tvpNames));

        var multi = expectedResultSetCount > 1;
        activity.SetTag(CaeriusDiagnostics.AttributeNames.ResultSetMulti, multi);
        activity.SetTag(CaeriusDiagnostics.AttributeNames.ResultSetExpectedCount, expectedResultSetCount);

        if (transactional)
            activity.SetTag(CaeriusDiagnostics.AttributeNames.Transactional, true);

        return activity;
    }

    /// <summary>
    ///     Builds the <see cref="TagList" /> shared between metrics and spans, identifying the
    ///     stored procedure, the calling operation, the TVP/multi-result-set context and whether the
    ///     call is transactional.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TagList BuildMetricTags(
        StoredProcedureParameters spParameters,
        string operation,
        bool transactional = false,
        int expectedResultSetCount = 1)
    {
        var tvpUsed = ExtractTvpTypeNames(spParameters.GetParametersSpan()).Count > 0;
        return new TagList
        {
            { CaeriusDiagnostics.AttributeNames.SpSchema, spParameters.SchemaName },
            { CaeriusDiagnostics.AttributeNames.SpName, spParameters.ProcedureName },
            { CaeriusDiagnostics.AttributeNames.SpCommand, operation },
            { CaeriusDiagnostics.AttributeNames.TvpUsed, tvpUsed },
            { CaeriusDiagnostics.AttributeNames.ResultSetMulti, expectedResultSetCount > 1 },
            { CaeriusDiagnostics.AttributeNames.Transactional, transactional }
        };
    }

    /// <summary>
    ///     Records a SQL error on the activity (as an OpenTelemetry exception event with status =
    ///     <see cref="ActivityStatusCode.Error" />) and increments the <c>caerius.sp.errors</c> counter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordError(Activity? activity, in TagList tags, Exception ex)
    {
        if (activity is not null)
        {
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.AddException(ex);
        }

        CaeriusDiagnostics.SpErrors.Add(1, tags);
    }

    /// <summary>
    ///     Records a successful execution: row count tag on the activity, duration histogram and
    ///     execution counter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordSuccess(
        Activity? activity,
        in TagList tags,
        double elapsedMs,
        int? rowsReturned = null,
        int? rowsAffected = null)
    {
        if (activity is not null)
        {
            if (rowsReturned.HasValue)
                activity.SetTag(CaeriusDiagnostics.AttributeNames.RowsReturned, rowsReturned.Value);
            if (rowsAffected.HasValue)
                activity.SetTag(CaeriusDiagnostics.AttributeNames.RowsAffected, rowsAffected.Value);
            activity.SetStatus(ActivityStatusCode.Ok);
        }

        CaeriusDiagnostics.SpDuration.Record(elapsedMs, tags);
        CaeriusDiagnostics.SpExecutions.Add(1, tags);
    }

    /// <summary>
    ///     Records a cache lookup outcome on the active span (if any) and on the
    ///     <c>caerius.cache.lookups</c> counter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordCacheLookup(StoredProcedureParameters spParameters, CacheType tier, bool hit)
    {
        var current = Activity.Current;
        if (current is not null && current.Source == CaeriusDiagnostics.ActivitySource)
        {
            current.SetTag(CaeriusDiagnostics.AttributeNames.CacheTier, tier.ToString());
            current.SetTag(CaeriusDiagnostics.AttributeNames.CacheHit, hit);
        }

        var tags = new TagList
        {
            { CaeriusDiagnostics.AttributeNames.SpSchema, spParameters.SchemaName },
            { CaeriusDiagnostics.AttributeNames.SpName, spParameters.ProcedureName },
            { CaeriusDiagnostics.AttributeNames.CacheTier, tier.ToString() },
            { CaeriusDiagnostics.AttributeNames.CacheHit, hit }
        };
        CaeriusDiagnostics.CacheLookups.Add(1, tags);
    }

    private static string JoinParameterNames(ReadOnlySpan<SqlParameter> parameters)
    {
        if (parameters.Length == 1)
            return parameters[0].ParameterName;

        var capacity = 0;
        for (var i = 0; i < parameters.Length; i++)
            capacity += parameters[i].ParameterName.Length + 1;

        return string.Create(capacity - 1, parameters.ToArray(), static (span, src) =>
        {
            var pos = 0;
            for (var i = 0; i < src.Length; i++)
            {
                if (i > 0)
                    span[pos++] = ',';
                var name = src[i].ParameterName.AsSpan();
                name.CopyTo(span[pos..]);
                pos += name.Length;
            }
        });
    }

    /// <summary>
    ///     Builds <c>@name=value</c> pairs for every parameter, separated by commas.
    ///     TVP parameters show <c>[TVP]</c> instead of the row data. Used only when
    ///     <see cref="CaeriusTelemetryOptions.CaptureParameterValues" /> is enabled.
    /// </summary>
    private static string JoinParameterNamesAndValues(ReadOnlySpan<SqlParameter> parameters)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            var p = parameters[i];
            sb.Append(p.ParameterName);
            sb.Append('=');
            sb.Append(FormatParameterValue(p));
        }

        return sb.ToString();
    }

    private static string FormatParameterValue(SqlParameter parameter)
    {
        if (parameter.SqlDbType == SqlDbType.Structured)
            return "[TVP]";
        if (parameter.Value is null or DBNull)
            return "(null)";
        return parameter.Value.ToString() ?? "(null)";
    }

    /// <summary>
    ///     Scans <paramref name="parameters" /> and collects the <see cref="SqlParameter.TypeName" />
    ///     of every Table-Valued Parameter (those with <see cref="SqlDbType.Structured" />).
    ///     Returns an empty list when no TVP is present. This avoids any additional state on
    ///     <see cref="StoredProcedureParameters" />.
    /// </summary>
    private static List<string> ExtractTvpTypeNames(ReadOnlySpan<SqlParameter> parameters)
    {
        List<string>? result = null;
        for (var i = 0; i < parameters.Length; i++)
        {
            ref readonly var p = ref parameters[i];
            if (p.SqlDbType == SqlDbType.Structured && !string.IsNullOrEmpty(p.TypeName))
                (result ??= new List<string>(2)).Add(p.TypeName);
        }

        return result ?? [];
    }

    /// <summary>
    ///     Wraps a multi-result-set stored-procedure execution with a CaeriusNet activity span and metrics.
    ///     <paramref name="expectedResultSetCount" /> is passed directly so that telemetry can report the
    ///     requested arity without modifying <see cref="StoredProcedureParameters" />.
    /// </summary>
    /// <typeparam name="T">Result tuple type produced by the executor.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static async ValueTask<T> InstrumentMultiResultSetAsync<T>(
        ICaeriusNetDbContext context,
        StoredProcedureParameters spParameters,
        int expectedResultSetCount,
        string operation,
        Func<SqlCommand, ValueTask<T>> execute,
        CancellationToken cancellationToken)
    {
        using var activity = StartStoredProcedureActivity(spParameters, operation,
            expectedResultSetCount: expectedResultSetCount);
        var tags = BuildMetricTags(spParameters, operation, expectedResultSetCount: expectedResultSetCount);
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            var result = await SqlCommandHelper.ExecuteCommandAsync(context, spParameters, execute, cancellationToken)
                .ConfigureAwait(false);

            var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            RecordSuccess(activity, tags, elapsedMs);
            return result;
        }
        catch (CaeriusNetSqlException ex)
        {
            RecordError(activity, tags, ex);
            throw;
        }
    }

    /// <summary>
    ///     Starts a parent <see cref="Activity" /> representing the lifetime of an
    ///     <see cref="ICaeriusNetTransaction" /> scope.  All stored-procedure activities
    ///     started while this activity is <see cref="Activity.Current" /> will be
    ///     automatically nested under it, so the Aspire dashboard shows a single
    ///     cohesive trace instead of one orphaned span per command.
    /// </summary>
    /// <param name="isolationLevel">SQL Server isolation level of the transaction.</param>
    /// <returns>
    ///     The started <see cref="Activity" />, or <see langword="null" /> when no listener is
    ///     subscribed (zero-cost path).
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Activity? StartTransactionActivity(IsolationLevel isolationLevel)
    {
        var source = CaeriusDiagnostics.ActivitySource;
        if (!source.HasListeners())
            return null;

        var activity = source.StartActivity("TX");
        activity?.SetTag(CaeriusDiagnostics.AttributeNames.TxIsolationLevel, isolationLevel.ToString());
        return activity;
    }

    /// <summary>
    ///     Marks the transaction activity with its final outcome and stops it.
    ///     A <see langword="null" /> <paramref name="activity" /> is a no-op.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordTransactionOutcome(Activity? activity, string outcome, bool isError = false)
    {
        if (activity is null)
            return;

        activity.SetTag(CaeriusDiagnostics.AttributeNames.TxOutcome, outcome);
        activity.SetStatus(isError ? ActivityStatusCode.Error : ActivityStatusCode.Ok, outcome);
        activity.Stop();
    }
}