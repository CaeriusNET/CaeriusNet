using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using CaeriusNet.Abstractions;
using CaeriusNet.Commands;
using CaeriusNet.Execution;
using CaeriusNet.Internal;
using CaeriusNet.Telemetry;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CaeriusNet.Tests.Observability;

[Collection(TelemetryTestScope.Name)]
public sealed class CaeriusInstrumentationTests
{
    [Fact]
    public void StartAndStopWithoutListenersAllocatesNoObservabilityObjects()
    {
        var command = CreateCommand();
        var options = new CaeriusNetOptions();
        using var instrumentation = new CaeriusInstrumentation();
        using var connection = new SqlConnection("Server=localhost;Database=telemetry");

        // JIT and static initialization must not be part of the allocation assertion.
        ExecuteSuccessfulOperation(instrumentation, command, connection, options);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 32; index++)
            ExecuteSuccessfulOperation(instrumentation, command, connection, options);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void DurationHistogramUsesOnlyBoundedDatabaseTags()
    {
        var measurements = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        using var listener = CreateMeterListener<double>("db.client.operation.duration", measurements);
        using var instrumentation = new CaeriusInstrumentation();
        using var connection = new SqlConnection("Server=telemetry-host;Database=tenant_42");

        ExecuteSuccessfulOperation(instrumentation, CreateCommand(), connection, new CaeriusNetOptions());

        var tags = Assert.Single(measurements).ToDictionary(static item => item.Key, static item => item.Value);
        Assert.Equal("microsoft.sql_server", tags["db.system.name"]);
        Assert.Equal("EXECUTE", tags["db.operation.name"]);
        Assert.DoesNotContain("db.stored_procedure.name", tags.Keys);
        Assert.DoesNotContain("db.namespace", tags.Keys);
        Assert.DoesNotContain("server.address", tags.Keys);
    }

    [Fact]
    public void CacheMetricsUseStaticTierNamesAndNeverExposeCacheKeys()
    {
        var measurements = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        using var listener = CreateMeterListener<long>("caerius.cache.lookups", measurements);
        using var instrumentation = new CaeriusInstrumentation();

        instrumentation.CacheLookup(CaeriusCacheTier.Memory, true);
        instrumentation.CacheLookup(CaeriusCacheTier.Frozen, false);
        instrumentation.CacheLookup(CaeriusCacheTier.Redis, true);

        var tags = measurements
            .Select(static items => items.ToDictionary(static item => item.Key, static item => item.Value))
            .ToArray();
        Assert.Equal(3, tags.Length);
        Assert.Equal(["frozen", "memory", "redis"], tags
            .Select(static item => (string)item["caerius.cache.tier"]!)
            .OrderBy(static tier => tier, StringComparer.Ordinal)
            .ToArray());
        Assert.All(tags, static item =>
        {
            Assert.Equal(2, item.Count);
            Assert.Contains("caerius.cache.hit", item.Keys);
            Assert.DoesNotContain(item.Keys, static key => key.Contains("key", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(item.Keys, static key => key.Contains("value", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public async Task MemoryReadThroughEmitsOneLookupMetricForEachMissAndHit()
    {
        var measurements = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        using var listener = CreateMeterListener<long>("caerius.cache.lookups", measurements);
        var services = new ServiceCollection();
        services.AddCaeriusNet(options => options.UseSqlServer(
            "Server=.;Database=telemetry_cache;Integrated Security=true;TrustServerCertificate=true"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = (ICaeriusExecutionContext)scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var command = new StoredProcedureCommandBuilder("dbo", "MemoryMetric")
            .UseInMemoryCache(TimeSpan.FromMinutes(1))
            .Build();
        var factoryCalls = 0;

        var first = await CaeriusExecutor.CachedAsync(
            context,
            command,
            "integer",
            _ => ValueTask.FromResult(Interlocked.Increment(ref factoryCalls)),
            TestContext.Current.CancellationToken);
        var second = await CaeriusExecutor.CachedAsync(
            context,
            command,
            "integer",
            _ => ValueTask.FromResult(Interlocked.Increment(ref factoryCalls)),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(1, factoryCalls);
        var tags = measurements
            .Select(static items => items.ToDictionary(static item => item.Key, static item => item.Value))
            .ToArray();
        Assert.Equal(2, tags.Length);
        Assert.All(tags, static measurement => Assert.Equal("memory", measurement["caerius.cache.tier"]));
        Assert.Equal([false, true], tags
            .Select(static measurement => (bool)measurement["caerius.cache.hit"]!)
            .ToArray());
    }

    [Fact]
    public void PropagationOnlyActivityDoesNotInvokeParameterRedaction()
    {
        var redactorCalls = 0;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == CaeriusTelemetry.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.PropagationData
        };
        ActivitySource.AddActivityListener(listener);
        var options = new CaeriusNetOptions
        {
            CaptureParameterValues = true,
            ParameterRedactor = (_, _) =>
            {
                Interlocked.Increment(ref redactorCalls);
                return "redacted";
            }
        };
        options.ParameterCaptureAllowList.Add("visible");
        var command = new StoredProcedureCommandBuilder("dbo", "Capture")
            .AddParameter("visible", "must-not-be-inspected", SqlDbType.NVarChar)
            .Build();

        using var instrumentation = new CaeriusInstrumentation();
        using var connection = new SqlConnection("Server=telemetry-host;Database=telemetry");
        ExecuteSuccessfulOperation(instrumentation, command, connection, options);

        Assert.Equal(0, redactorCalls);
    }

    [Fact]
    public void ActivityFailureExportsClassificationButNeverItsMessage()
    {
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == CaeriusTelemetry.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = stopped.Enqueue
        };
        ActivitySource.AddActivityListener(listener);
        using var instrumentation = new CaeriusInstrumentation();
        using var connection = new SqlConnection("Server=telemetry-host;Database=telemetry");
        const string secret = "cache-key=super-secret";

        using (var operation = instrumentation.Start(CreateCommand(), false, new CaeriusNetOptions()))
        {
            operation.SetConnection(connection);
            operation.Failure(new InvalidOperationException(secret));
        }

        var activity = Assert.Single(stopped);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.StatusDescription);
        Assert.Equal(typeof(InvalidOperationException).FullName, activity.GetTagItem("error.type"));
        Assert.Equal("telemetry", activity.GetTagItem("db.namespace"));
        Assert.Equal("telemetry-host", activity.GetTagItem("server.address"));
        Assert.DoesNotContain(secret, activity.TagObjects.Select(static tag => tag.Value?.ToString()));
    }

    [Fact]
    public void RuntimeLogsAreGeneratedAndContainOnlySafeStructuredFields()
    {
        var logger = new CapturingLogger();

        CaeriusRuntimeLog.RedisLookupFailed(
            logger,
            "dbo.GetWidgets",
            typeof(InvalidOperationException).FullName!);

        Assert.Null(logger.Exception);
        Assert.Equal("dbo.GetWidgets", logger.State["Procedure"]);
        Assert.Equal(typeof(InvalidOperationException).FullName, logger.State["ErrorType"]);
        Assert.DoesNotContain(logger.State.Keys, static key => key.Contains("key", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logger.State.Keys,
            static key => key.Contains("value", StringComparison.OrdinalIgnoreCase));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ExecuteSuccessfulOperation(
        CaeriusInstrumentation instrumentation,
        StoredProcedureCommand command,
        SqlConnection connection,
        CaeriusNetOptions options)
    {
        using var operation = instrumentation.Start(command, connection, false, options);
        operation.Success();
    }

    private static StoredProcedureCommand CreateCommand()
    {
        return new StoredProcedureCommandBuilder("dbo", "TelemetryProcedure").Build();
    }

    private static MeterListener CreateMeterListener<T>(
        string instrumentName,
        ConcurrentQueue<KeyValuePair<string, object?>[]> measurements)
        where T : struct
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CaeriusTelemetry.MeterName && instrument.Name == instrumentName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<T>((_, _, tags, _) => measurements.Enqueue(tags.ToArray()));
        listener.Start();
        return listener;
    }

    private sealed class CapturingLogger : ILogger
    {
        public Exception? Exception { get; private set; }
        public Dictionary<string, object?> State { get; } = new(StringComparer.Ordinal);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Exception = exception;
            if (state is IEnumerable<KeyValuePair<string, object?>> properties)
                foreach (var property in properties)
                    State[property.Key] = property.Value;
        }
    }
}
