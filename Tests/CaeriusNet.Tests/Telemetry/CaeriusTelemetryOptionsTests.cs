using System.Diagnostics;

namespace CaeriusNet.Tests.Telemetry;

/// <summary>
///     Verifies <see cref="CaeriusTelemetryOptions" /> and its integration with
///     <see cref="CaeriusDiagnostics.TelemetryOptions" /> and
///     <see cref="CaeriusActivityExtensions.StartStoredProcedureActivity" />.
/// </summary>
[Collection(TelemetryTestsCollection.Name)]
public sealed class CaeriusTelemetryOptionsTests : IDisposable
{
    private readonly CaeriusTelemetryOptions _previousOptions = CaeriusDiagnostics.TelemetryOptions;

    public void Dispose()
    {
        // Restore the global options after each test to avoid cross-test pollution.
        CaeriusDiagnostics.TelemetryOptions = _previousOptions;
    }

    private static StoredProcedureParameters SpWithParam(string name, object value, SqlDbType type)
    {
        var param = new SqlParameter(name, type) { Value = value };
        return new StoredProcedureParameters("Users", "usp_Test", 1, [param], null, null, null);
    }

    private static StoredProcedureParameters SpWithTvp(string name)
    {
        var param = new SqlParameter(name, SqlDbType.Structured) { TypeName = "Users.TestTvp" };
        return new StoredProcedureParameters("Users", "usp_Test", 1, [param], null, null, null);
    }

    private static (List<Activity> activities, ActivityListener listener) StartListener()
    {
        var captured = new List<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CaeriusDiagnostics.SourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => captured.Add(a)
        };
        ActivitySource.AddActivityListener(listener);
        return (captured, listener);
    }

    [Fact]
    public void Default_CaptureParameterValues_IsFalse()
    {
        var options = new CaeriusTelemetryOptions();
        Assert.False(options.CaptureParameterValues);
    }

    [Fact]
    public void WhenCaptureParameterValues_False_TagContainsOnlyNames()
    {
        CaeriusDiagnostics.TelemetryOptions = new CaeriusTelemetryOptions { CaptureParameterValues = false };

        var (captured, listener) = StartListener();
        try
        {
            var sp = SpWithParam("@userId", 42, SqlDbType.Int);
            using (CaeriusActivityExtensions.StartStoredProcedureActivity(sp, "FirstQueryAsync"))
            {
            }

            var tag = captured.Single().TagObjects
                .Single(t => t.Key == CaeriusDiagnostics.AttributeNames.SpParameters).Value as string;
            Assert.Equal("@userId", tag);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public void WhenCaptureParameterValues_True_TagContainsNamesAndValues()
    {
        CaeriusDiagnostics.TelemetryOptions = new CaeriusTelemetryOptions { CaptureParameterValues = true };

        var (captured, listener) = StartListener();
        try
        {
            var sp = SpWithParam("@userId", 42, SqlDbType.Int);
            using (CaeriusActivityExtensions.StartStoredProcedureActivity(sp, "FirstQueryAsync"))
            {
            }

            var tag = captured.Single().TagObjects
                .Single(t => t.Key == CaeriusDiagnostics.AttributeNames.SpParameters).Value as string;
            Assert.Equal("@userId=42", tag);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public void WhenCaptureParameterValues_True_TvpShowsPlaceholder()
    {
        CaeriusDiagnostics.TelemetryOptions = new CaeriusTelemetryOptions { CaptureParameterValues = true };

        var (captured, listener) = StartListener();
        try
        {
            var sp = SpWithTvp("@ids");
            using (CaeriusActivityExtensions.StartStoredProcedureActivity(sp, "QueryAsImmutableArrayAsync"))
            {
            }

            var tag = captured.Single().TagObjects
                .Single(t => t.Key == CaeriusDiagnostics.AttributeNames.SpParameters).Value as string;
            Assert.Equal("@ids=[TVP]", tag);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public void WhenCaptureParameterValues_True_NullValueShowsNullPlaceholder()
    {
        CaeriusDiagnostics.TelemetryOptions = new CaeriusTelemetryOptions { CaptureParameterValues = true };

        var (captured, listener) = StartListener();
        try
        {
            var sp = SpWithParam("@name", DBNull.Value, SqlDbType.NVarChar);
            using (CaeriusActivityExtensions.StartStoredProcedureActivity(sp, "FirstQueryAsync"))
            {
            }

            var tag = captured.Single().TagObjects
                .Single(t => t.Key == CaeriusDiagnostics.AttributeNames.SpParameters).Value as string;
            Assert.Equal("@name=(null)", tag);
        }
        finally
        {
            listener.Dispose();
        }
    }
}
