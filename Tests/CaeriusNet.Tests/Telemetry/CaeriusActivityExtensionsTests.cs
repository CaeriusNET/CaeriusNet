using System.Diagnostics;

namespace CaeriusNet.Tests.Telemetry;

/// <summary>
///     Verifies the <see cref="CaeriusActivityExtensions" /> helpers create OpenTelemetry-compatible
///     spans with the expected <c>db.*</c> + <c>caerius.*</c> tags, and react correctly to error,
///     success and cache-lookup signals. These tests do not require a SQL Server.
/// </summary>
[Collection(TelemetryTestsCollection.Name)]
public sealed class CaeriusActivityExtensionsTests
{
    /// <summary>Builds a <see cref="StoredProcedureParameters" /> with optional raw parameters.</summary>
    private static StoredProcedureParameters BuildSp(SqlParameter[]? parameters = null)
    {
        return new StoredProcedureParameters(
            "Users",
            "usp_GetUsers",
            16,
            parameters ?? [],
            null,
            null,
            null);
    }

    /// <summary>Builds a structured <see cref="SqlParameter" /> that acts as a TVP.</summary>
    private static SqlParameter Tvp(string paramName, string typeName)
    {
        return new SqlParameter(paramName, SqlDbType.Structured) { TypeName = typeName };
    }

    private static (List<Activity> activities, ActivityListener listener) StartListener()
    {
        var captured = new List<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CaeriusDiagnostics.SourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => captured.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        return (captured, listener);
    }

    [Fact]
    public void StartStoredProcedureActivity_NoListener_ReturnsNull()
    {
        var sp = BuildSp();

        var activity = CaeriusActivityExtensions.StartStoredProcedureActivity(sp, "FirstQueryAsync");

        Assert.Null(activity);
    }

    [Fact]
    public void StartStoredProcedureActivity_SetsSchemaProcedureAndDbTags()
    {
        var (captured, listener) = StartListener();
        try
        {
            var sp = BuildSp([new SqlParameter("@id", SqlDbType.Int)]);

            using (var activity = CaeriusActivityExtensions.StartStoredProcedureActivity(sp, "FirstQueryAsync"))
            {
                Assert.NotNull(activity);
                Assert.Equal(ActivityKind.Client, activity.Kind);
                Assert.Equal("SP Users.usp_GetUsers", activity.OperationName);
            }

            var stopped = Assert.Single(captured);
            var tags = stopped.TagObjects.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Assert.Equal("mssql", tags[CaeriusDiagnostics.AttributeNames.DbSystem]);
            Assert.Equal("FirstQueryAsync", tags[CaeriusDiagnostics.AttributeNames.DbOperation]);
            Assert.Equal("Users.usp_GetUsers", tags[CaeriusDiagnostics.AttributeNames.DbStatement]);
            Assert.Equal("Users", tags[CaeriusDiagnostics.AttributeNames.SpSchema]);
            Assert.Equal("usp_GetUsers", tags[CaeriusDiagnostics.AttributeNames.SpName]);
            Assert.Equal("FirstQueryAsync", tags[CaeriusDiagnostics.AttributeNames.SpCommand]);
            Assert.Equal("@id", tags[CaeriusDiagnostics.AttributeNames.SpParameters]);
            Assert.Equal(false, tags[CaeriusDiagnostics.AttributeNames.TvpUsed]);
            Assert.Equal(false, tags[CaeriusDiagnostics.AttributeNames.ResultSetMulti]);
            Assert.Equal(1, tags[CaeriusDiagnostics.AttributeNames.ResultSetExpectedCount]);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public void StartStoredProcedureActivity_TvpUsage_DetectedFromStructuredParameter()
    {
        var (captured, listener) = StartListener();
        try
        {
            var sp = BuildSp([Tvp("@tvp", "Users.UsersIntTvp")]);

            using (CaeriusActivityExtensions.StartStoredProcedureActivity(sp, "QueryAsImmutableArrayAsync"))
            {
            }

            var tags = captured.Single().TagObjects.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Assert.Equal(true, tags[CaeriusDiagnostics.AttributeNames.TvpUsed]);
            Assert.Equal("Users.UsersIntTvp", tags[CaeriusDiagnostics.AttributeNames.TvpTypeName]);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public void StartStoredProcedureActivity_MultipleTvps_AreCommaSeparated()
    {
        var (captured, listener) = StartListener();
        try
        {
            var sp = BuildSp([Tvp("@a", "Users.A"), Tvp("@b", "Users.B")]);

            using (CaeriusActivityExtensions.StartStoredProcedureActivity(sp, "ExecuteAsync"))
            {
            }

            var tag = captured.Single().TagObjects
                .Single(t => t.Key == CaeriusDiagnostics.AttributeNames.TvpTypeName).Value;
            Assert.Equal("Users.A,Users.B", tag);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public void StartStoredProcedureActivity_MultiResultSet_IsReflectedInTags()
    {
        var (captured, listener) = StartListener();
        try
        {
            var sp = BuildSp();

            using (CaeriusActivityExtensions.StartStoredProcedureActivity(sp, "QueryMultipleImmutableArrayAsync",
                       expectedResultSetCount: 3))
            {
            }

            var tags = captured.Single().TagObjects.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Assert.Equal(true, tags[CaeriusDiagnostics.AttributeNames.ResultSetMulti]);
            Assert.Equal(3, tags[CaeriusDiagnostics.AttributeNames.ResultSetExpectedCount]);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public void StartStoredProcedureActivity_Transactional_AddsCaeriusTxTag()
    {
        var (captured, listener) = StartListener();
        try
        {
            var sp = BuildSp();

            using (CaeriusActivityExtensions.StartStoredProcedureActivity(sp, "ExecuteAsync", true))
            {
            }

            var tags = captured.Single().TagObjects.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Assert.Equal(true, tags[CaeriusDiagnostics.AttributeNames.Transactional]);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public void StartStoredProcedureActivity_NoParameters_OmitsParametersTag()
    {
        var (captured, listener) = StartListener();
        try
        {
            var sp = BuildSp();

            using (CaeriusActivityExtensions.StartStoredProcedureActivity(sp, "ExecuteAsync"))
            {
            }

            Assert.DoesNotContain(captured.Single().TagObjects,
                t => t.Key == CaeriusDiagnostics.AttributeNames.SpParameters);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public void RecordSuccess_SetsRowsAndOkStatus()
    {
        var (captured, listener) = StartListener();
        try
        {
            var sp = BuildSp();
            using (var activity = CaeriusActivityExtensions.StartStoredProcedureActivity(sp, "FirstQueryAsync"))
            {
                var tags = CaeriusActivityExtensions.BuildMetricTags(sp, "FirstQueryAsync");
                CaeriusActivityExtensions.RecordSuccess(activity, tags, 12.5, 42);
            }

            var stopped = captured.Single();
            Assert.Equal(ActivityStatusCode.Ok, stopped.Status);
            var rows = stopped.TagObjects.Single(t => t.Key == CaeriusDiagnostics.AttributeNames.RowsReturned).Value;
            Assert.Equal(42, rows);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public void RecordError_SetsErrorStatusAndAttachesException()
    {
        var (captured, listener) = StartListener();
        try
        {
            var sp = BuildSp();
            var error = new InvalidOperationException("boom");
            using (var activity = CaeriusActivityExtensions.StartStoredProcedureActivity(sp, "FirstQueryAsync"))
            {
                var tags = CaeriusActivityExtensions.BuildMetricTags(sp, "FirstQueryAsync");
                CaeriusActivityExtensions.RecordError(activity, tags, error);
            }

            var stopped = captured.Single();
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
            Assert.Equal("boom", stopped.StatusDescription);
            Assert.Contains(stopped.Events, e => e.Name == "exception");
        }
        finally
        {
            listener.Dispose();
        }
    }
}