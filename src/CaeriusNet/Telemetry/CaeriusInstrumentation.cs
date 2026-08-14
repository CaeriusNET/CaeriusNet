namespace CaeriusNet;

internal sealed class CaeriusInstrumentation : IDisposable
{
    private static readonly KeyValuePair<string, object?> DatabaseSystemTag =
        new("db.system.name", "microsoft.sql_server");

    private static readonly KeyValuePair<string, object?> ExecuteOperationTag = new("db.operation.name", "EXECUTE");

    private static readonly string InstrumentationVersion =
        typeof(CaeriusInstrumentation).Assembly.GetName().Version?.ToString(3) ?? "12.0.0";

    private readonly ActivitySource _activitySource = new(CaeriusTelemetry.ActivitySourceName, InstrumentationVersion);
    private readonly Counter<long> _cacheErrors;
    private readonly Counter<long> _cacheLookups;
    private readonly Histogram<double> _duration;
    private readonly Meter _meter = new(CaeriusTelemetry.MeterName, InstrumentationVersion);

    public CaeriusInstrumentation()
    {
        _duration = _meter.CreateHistogram<double>("db.client.operation.duration", "s", "Stored procedure duration.");
        _cacheLookups = _meter.CreateCounter<long>("caerius.cache.lookups", "{lookup}");
        _cacheErrors = _meter.CreateCounter<long>("caerius.cache.errors", "{error}");
    }

    public void Dispose()
    {
        _activitySource.Dispose();
        _meter.Dispose();
    }

    public Operation Start(
        StoredProcedureCommand command,
        SqlConnection connection,
        bool transactional,
        CaeriusNetOptions options)
    {
        var operation = Start(command, transactional, options);
        operation.SetConnection(connection);
        return operation;
    }

    /// <summary>
    ///     Starts the logical stored-procedure operation before connection acquisition.  The executor can therefore
    ///     report a lease/open failure and include that latency; it calls <see cref="Operation.SetConnection" /> after a
    ///     connection has been acquired to enrich the activity with the server and namespace.
    /// </summary>
    public Operation Start(
        StoredProcedureCommand command,
        bool transactional,
        CaeriusNetOptions options)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(options);
        Activity? activity = null;
        if (_activitySource.HasListeners())
        {
            activity = _activitySource.StartActivity(
                string.Concat("EXECUTE ", command.FullName),
                ActivityKind.Client);
            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag("db.system.name", "microsoft.sql_server");
                activity.SetTag("db.operation.name", "EXECUTE");
                activity.SetTag("db.stored_procedure.name", command.FullName);
                activity.SetTag("caerius.transactional", transactional);
                CaptureAllowedParameters(activity, command, options);
            }
        }

        var recordDuration = _duration.Enabled;
        return activity is null && !recordDuration
            ? default
            : new Operation(activity, _duration, recordDuration ? Stopwatch.GetTimestamp() : 0L, recordDuration);
    }

    public void CacheLookup(CaeriusCacheTier tier, bool hit)
    {
        if (!_cacheLookups.Enabled)
            return;

        _cacheLookups.Add(1,
            new KeyValuePair<string, object?>("caerius.cache.tier", GetCacheTierName(tier)),
            new KeyValuePair<string, object?>("caerius.cache.hit", hit));
    }

    public void CacheError(CaeriusCacheTier tier)
    {
        if (!_cacheErrors.Enabled)
            return;

        _cacheErrors.Add(1, new KeyValuePair<string, object?>("caerius.cache.tier", GetCacheTierName(tier)));
    }

    private static void CaptureAllowedParameters(
        Activity activity,
        StoredProcedureCommand command,
        CaeriusNetOptions options)
    {
        if (!options.CaptureParameterValues || options.ParameterRedactor is null)
            return;

        foreach (var parameter in command.Parameters)
        {
            // TVPs can contain arbitrarily many sensitive values.  They are never exported, even when a parameter
            // name happens to appear in the development allowlist.
            if (parameter.DbType == SqlDbType.Structured ||
                parameter.Direction is not (ParameterDirection.Input or ParameterDirection.InputOutput))
                continue;

            var bareName = parameter.Name.TrimStart('@');
            if (!options.ParameterCaptureAllowList.Contains(parameter.Name) &&
                !options.ParameterCaptureAllowList.Contains(bareName))
                continue;

            try
            {
                activity.SetTag($"caerius.db.parameter.{bareName}",
                    options.ParameterRedactor(parameter.Name, parameter.Value));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Observability must remain strictly best-effort.  A faulty redactor cannot fail a database call or
                // result in an unredacted fallback value being exported.
            }
        }
    }

    private static string GetCacheTierName(CaeriusCacheTier tier)
    {
        return tier switch
        {
            CaeriusCacheTier.Memory => "memory",
            CaeriusCacheTier.Frozen => "frozen",
            CaeriusCacheTier.Redis => "redis",
            _ => "unknown"
        };
    }

    /// <summary>
    ///     Value-type handle returned on every command. When no telemetry listener exists it is the default value,
    ///     so executing a command does not allocate a span name, a state object, or a timestamp for observability.
    /// </summary>
    internal readonly struct Operation : IDisposable
    {
        private readonly OperationState? _state;

        internal Operation(Activity? activity, Histogram<double> duration, long started, bool recordDuration)
        {
            _state = new OperationState(activity, duration, started, recordDuration);
        }

        public void Success(int? statusCode = null)
        {
            _state?.Success(statusCode);
        }

        public void Failure(Exception exception)
        {
            _state?.Failure(exception);
        }

        public void SetConnection(SqlConnection connection)
        {
            _state?.SetConnection(connection);
        }

        public void Dispose()
        {
            _state?.Stop();
        }
    }

    private sealed class OperationState(
        Activity? activity,
        Histogram<double> duration,
        long started,
        bool recordDuration)
    {
        private bool _completed;

        public void Success(int? statusCode)
        {
            if (statusCode is { } value)
                activity?.SetTag("db.response.status_code", value);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _completed = true;
        }

        public void Failure(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            activity?.SetTag("error.type", exception.GetType().FullName ?? exception.GetType().Name);
            if (exception is CaeriusNetSqlException sql)
                activity?.SetTag("db.response.status_code", sql.Number);
            // Error messages can contain SQL values or details injected by a custom mapper/codec.  The exception
            // type and SQL error number remain enough to classify the failure without exporting such values.
            activity?.SetStatus(ActivityStatusCode.Error);
            _completed = true;
        }

        public void SetConnection(SqlConnection connection)
        {
            ArgumentNullException.ThrowIfNull(connection);
            if (activity?.IsAllDataRequested != true)
                return;

            activity.SetTag("db.namespace", connection.Database);
            activity.SetTag("server.address", connection.DataSource);
        }

        public void Stop()
        {
            if (recordDuration)
            {
                var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
                // Procedure names belong to tracing.  Omitting them from the histogram deliberately keeps the
                // metric cardinality bounded for applications with generated or tenant-specific procedures.
                duration.Record(seconds, DatabaseSystemTag, ExecuteOperationTag);
            }

            if (!_completed)
                activity?.SetStatus(ActivityStatusCode.Unset);
            activity?.Dispose();
        }
    }
}
