using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace CaeriusNet.Tests.Telemetry;

/// <summary>
///     Verifies that CaeriusNet metric instruments are reachable through a
///     <see cref="MeterListener" /> and emit the expected tags.
/// </summary>
[Collection(TelemetryTestsCollection.Name)]
public sealed class CaeriusDiagnosticsMetricsTests
{
    private static StoredProcedureParameters Sp()
    {
        return new StoredProcedureParameters(
            "Users", "usp_Test", 16, [], null, null, null);
    }

    private static MeterListener StartListener<T>(string instrumentName, MeasurementCapture<T> capture)
        where T : struct
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == CaeriusDiagnostics.SourceName && instrument.Name == instrumentName)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<T>((instrument, value, tags, _) =>
            capture.Items.Add((value, tags.ToArray())));
        listener.Start();
        return listener;
    }

    [Fact]
    public void RecordSuccess_EmitsDurationAndExecutions()
    {
        var durationCapture = new MeasurementCapture<double>();
        var execCapture = new MeasurementCapture<long>();
        using var dl = StartListener("caerius.sp.duration", durationCapture);
        using var el = StartListener("caerius.sp.executions", execCapture);

        var sp = Sp();
        var tags = CaeriusActivityExtensions.BuildMetricTags(sp, "FirstQueryAsync");
        CaeriusActivityExtensions.RecordSuccess(null, tags, 8.0, 5);

        Assert.Single(durationCapture.Items);
        Assert.Equal(8.0, durationCapture.Items.Single().value);
        Assert.Single(execCapture.Items);

        var execTags = execCapture.Items.Single().tags
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Equal("Users", execTags[CaeriusDiagnostics.AttributeNames.SpSchema]);
        Assert.Equal("usp_Test", execTags[CaeriusDiagnostics.AttributeNames.SpName]);
        Assert.Equal("FirstQueryAsync", execTags[CaeriusDiagnostics.AttributeNames.SpCommand]);
        Assert.Equal(false, execTags[CaeriusDiagnostics.AttributeNames.TvpUsed]);
        Assert.Equal(false, execTags[CaeriusDiagnostics.AttributeNames.ResultSetMulti]);
        Assert.Equal(false, execTags[CaeriusDiagnostics.AttributeNames.Transactional]);
    }

    [Fact]
    public void RecordError_EmitsErrorCounter()
    {
        var capture = new MeasurementCapture<long>();
        using var listener = StartListener("caerius.sp.errors", capture);

        var sp = Sp();
        var tags = CaeriusActivityExtensions.BuildMetricTags(sp, "ExecuteAsync", true);
        CaeriusActivityExtensions.RecordError(null, tags, new InvalidOperationException("err"));

        var item = Assert.Single(capture.Items);
        Assert.Equal(1L, item.value);
        var tagDict = item.tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Equal(true, tagDict[CaeriusDiagnostics.AttributeNames.Transactional]);
    }

    [Fact]
    public void RecordCacheLookup_HitAndMiss_AreTagged()
    {
        var capture = new MeasurementCapture<long>();
        using var listener = StartListener("caerius.cache.lookups", capture);

        var sp = Sp();
        CaeriusActivityExtensions.RecordCacheLookup(sp, CacheType.InMemory, true);
        CaeriusActivityExtensions.RecordCacheLookup(sp, CacheType.Redis, false);

        Assert.Equal(2, capture.Items.Count);

        var hits = capture.Items
            .Select(i => i.tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
            .ToList();

        Assert.Contains(hits, t =>
            (string)t[CaeriusDiagnostics.AttributeNames.CacheTier]! == "InMemory" &&
            (bool)t[CaeriusDiagnostics.AttributeNames.CacheHit]!);
        Assert.Contains(hits, t =>
            (string)t[CaeriusDiagnostics.AttributeNames.CacheTier]! == "Redis" &&
            !(bool)t[CaeriusDiagnostics.AttributeNames.CacheHit]!);
    }

    [Fact]
    public void SourceNameAndVersion_ArePopulated()
    {
        Assert.Equal("CaeriusNet", CaeriusDiagnostics.SourceName);
        Assert.False(string.IsNullOrEmpty(CaeriusDiagnostics.SourceVersion));
        Assert.Equal(CaeriusDiagnostics.SourceName, CaeriusDiagnostics.ActivitySource.Name);
        Assert.Equal(CaeriusDiagnostics.SourceName, CaeriusDiagnostics.Meter.Name);
    }

    private sealed class MeasurementCapture<T> where T : struct
    {
        public ConcurrentBag<(T value, KeyValuePair<string, object?>[] tags)> Items { get; } = new();
    }
}