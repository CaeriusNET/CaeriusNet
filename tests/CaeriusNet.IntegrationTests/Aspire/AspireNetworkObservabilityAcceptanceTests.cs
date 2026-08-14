using System.Collections.Concurrent;
using System.Diagnostics;
using CaeriusNet.Aspire;
using CaeriusNet.Execution;
using CaeriusNet.IntegrationTests.Infrastructure;
using CaeriusNet.Telemetry;
using CaeriusNet.Transactions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace CaeriusNet.IntegrationTests.Aspire;

/// <summary>
///     Network-level acceptance tests for the Aspire bridge. They are deliberately opt-in with the rest of the
///     Testcontainers matrix because they exercise the real SqlClient and StackExchange.Redis transports rather than
///     a connection-string-only registration path.
/// </summary>
public sealed class AspireNetworkObservabilityAcceptanceTests
{
    private const string Secret = "caerius-network-secret";

    [Theory]
    [MemberData(nameof(ContainerTestGate.SqlServerImages), MemberType = typeof(ContainerTestGate))]
    public async Task DefaultAspirePathUsesOneCaeriusSpanKeepsTransportSpansOffAndOwnsTransactionLeases(
        string image)
    {
        ContainerTestGate.RequireEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var sqlServer = new MsSqlBuilder(image)
            .WithPassword("Caerius!Aspire_2026")
            .Build();
        await using var redis = new RedisBuilder("redis:7.4-alpine").Build();
        await sqlServer.StartAsync(cancellationToken);
        await redis.StartAsync(cancellationToken);

        var sqlConnectionString = CreateSqlConnectionString(sqlServer.GetConnectionString());
        await ProvisionAsync(sqlConnectionString, cancellationToken);

        var activities = new CollectingActivityProcessor();
        var leaseScopes = new LeaseScopeTracker();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:sql"] = sqlConnectionString,
            ["ConnectionStrings:cache"] = redis.GetConnectionString()
        });
        builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddProcessor(activities));
        builder.AddCaeriusNetAspire(
            "sql",
            options =>
            {
                options.ApplicationNamespace = "aspire-network";
                options.CaptureParameterValues = true;
                options.ParameterCaptureAllowList.Add("@secret");
                options.ParameterRedactor = static (_, _) => "<redacted>";
            },
            options => options.EnableSqlServerVersionHealthCheck = true,
            "cache");

        // The probe is deliberately resolved by the replacement scoped connection factory. It makes the scope that
        // Aspire creates internally observable without changing CaeriusNet's lease contract or opening fake SQL
        // connections: each factory result is a real SqlConnection to the Testcontainers server.
        builder.Services.AddSingleton(leaseScopes);
        builder.Services.AddScoped<LeaseScopeProbe>();
        builder.Services.AddScoped<SqlConnection>(provider =>
        {
            _ = provider.GetRequiredService<LeaseScopeProbe>();
            return new SqlConnection(sqlConnectionString);
        });

        using var host = builder.Build();
        _ = host.Services.GetRequiredService<TracerProvider>();
        Assert.True(await host.Services.GetRequiredService<IConnectionMultiplexer>()
            .GetDatabase()
            .PingAsync()
            .WaitAsync(cancellationToken) > TimeSpan.Zero);

        var healthRegistration = Assert.Single(
            host.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations,
            static registration => registration.Name == "caerius-sqlserver-version-default");
        var healthCheck = healthRegistration.Factory(host.Services);
        var firstHealth = await healthCheck.CheckHealthAsync(new HealthCheckContext(), cancellationToken);
        Assert.Equal(HealthStatus.Healthy, firstHealth.Status);
        Assert.NotNull(firstHealth.Description);
        var firstDescription = firstHealth.Description!;
        Assert.True(
            firstDescription.Contains("major version 16", StringComparison.Ordinal) ||
            firstDescription.Contains("major version 17", StringComparison.Ordinal));
        Assert.Equal(1, leaseScopes.Created);
        Assert.Equal(1, leaseScopes.Disposed);

        await using var applicationScope = host.Services.CreateAsyncScope();
        var database = applicationScope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var scalarCommand = new StoredProcedureCommandBuilder("dbo", "caerius_aspire_observability")
            .AddParameter("@value", 41, SqlDbType.Int)
            .AddParameter("@secret", Secret, SqlDbType.NVarChar, size: 128)
            .Build();

        activities.Clear();
        Assert.Equal(42, await database.ExecuteScalarAsync<int>(scalarCommand, cancellationToken));
        var successSpan = AssertSingleCaeriusSpan(activities.Snapshot());
        AssertStableSuccessTags(successSpan, "dbo.caerius_aspire_observability");
        Assert.Equal("<redacted>", successSpan.Tags["caerius.db.parameter.secret"]);
        Assert.DoesNotContain(successSpan.Tags, static tag =>
            tag.Value is string value && value.Contains(Secret, StringComparison.Ordinal));
        Assert.Equal(2, leaseScopes.Created);
        Assert.Equal(2, leaseScopes.Disposed);

        activities.Clear();
        var sqlFailure = await Assert.ThrowsAsync<CaeriusNetSqlException>(async () =>
            await database.ExecuteScalarAsync<int>(
                new StoredProcedureCommandBuilder("dbo", "caerius_aspire_observability_failure").Build(),
                cancellationToken));
        Assert.Equal(51000, sqlFailure.Number);
        var failureSpan = AssertSingleCaeriusSpan(activities.Snapshot());
        Assert.Equal(ActivityStatusCode.Error, failureSpan.Status);
        Assert.Null(failureSpan.StatusDescription);
        Assert.Equal(typeof(CaeriusNetSqlException).FullName, failureSpan.Tags["error.type"]);
        Assert.Equal(51000, failureSpan.Tags["db.response.status_code"]);
        Assert.DoesNotContain(failureSpan.Tags, static tag =>
            tag.Value is string value && value.Contains(Secret, StringComparison.Ordinal));

        var transactionCommand = new StoredProcedureCommandBuilder("dbo", "caerius_aspire_session").Build();
        await using (var transaction = await database.BeginTransactionAsync(cancellationToken: cancellationToken))
        {
            Assert.Equal(4, leaseScopes.Created);
            Assert.Equal(3, leaseScopes.Disposed);
            var firstSession = await transaction.ExecuteScalarAsync<int>(transactionCommand, cancellationToken);
            var secondSession = await transaction.ExecuteScalarAsync<int>(transactionCommand, cancellationToken);
            Assert.Equal(firstSession, secondSession);
            await transaction.CommitAsync(cancellationToken);
        }

        Assert.Equal(4, leaseScopes.Created);
        Assert.Equal(4, leaseScopes.Disposed);

        // The second result stays healthy after the real database has gone away. This is stronger than merely
        // comparing two successful calls: it proves the registry's one-time version result is used on the second
        // health check and no connection is opened again.
        await sqlServer.StopAsync(cancellationToken);
        var cachedHealth = await healthCheck.CheckHealthAsync(new HealthCheckContext(), cancellationToken);
        Assert.Equal(HealthStatus.Healthy, cachedHealth.Status);
        Assert.Equal(firstDescription, cachedHealth.Description);
        Assert.Equal(4, leaseScopes.Created);
    }

    [Theory]
    [MemberData(nameof(ContainerTestGate.SqlServerImages), MemberType = typeof(ContainerTestGate))]
    public async Task LowLevelSqlClientTracingIsAnExplicitChildSpanOptIn(string image)
    {
        ContainerTestGate.RequireEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var sqlServer = new MsSqlBuilder(image)
            .WithPassword("Caerius!Aspire_2026")
            .Build();
        await sqlServer.StartAsync(cancellationToken);
        var sqlConnectionString = CreateSqlConnectionString(sqlServer.GetConnectionString());
        await ProvisionAsync(sqlConnectionString, cancellationToken);

        var activities = new CollectingActivityProcessor();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:sql"] = sqlConnectionString
        });
        builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddProcessor(activities));
        builder.AddCaeriusNetAspire(
            "sql",
            configureAspire: options => options.EnableLowLevelSqlClientTracing = true);
        using var host = builder.Build();
        _ = host.Services.GetRequiredService<TracerProvider>();
        await using var scope = host.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        activities.Clear();
        Assert.Equal(8, await database.ExecuteScalarAsync<int>(
            new StoredProcedureCommandBuilder("dbo", "caerius_aspire_observability")
                .AddParameter("@value", 7, SqlDbType.Int)
                .AddParameter("@secret", "not-exported", SqlDbType.NVarChar, size: 128)
                .Build(),
            cancellationToken));

        var spans = activities.Snapshot();
        var caerius = Assert.Single(spans, static span => span.SourceName == CaeriusTelemetry.ActivitySourceName);
        var sqlClient = Assert.Single(spans, static span => span.SourceName != CaeriusTelemetry.ActivitySourceName);
        Assert.Contains("SqlClient", sqlClient.SourceName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(caerius.TraceId, sqlClient.TraceId);
        Assert.Equal(caerius.SpanId, sqlClient.ParentSpanId);
    }

    private static ActivitySnapshot AssertSingleCaeriusSpan(IReadOnlyList<ActivitySnapshot> spans)
    {
        var span = Assert.Single(spans,
            static candidate => candidate.SourceName == CaeriusTelemetry.ActivitySourceName);
        Assert.Single(spans);
        return span;
    }

    private static void AssertStableSuccessTags(ActivitySnapshot span, string procedure)
    {
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal("microsoft.sql_server", span.Tags["db.system.name"]);
        Assert.Equal("EXECUTE", span.Tags["db.operation.name"]);
        Assert.Equal(procedure, span.Tags["db.stored_procedure.name"]);
        Assert.Equal("master", span.Tags["db.namespace"]);
        Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<string>(span.Tags["server.address"])));
        Assert.DoesNotContain(span.Tags.Keys,
            static key => key.Contains("connection", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(span.Tags.Keys, static key => key.Contains("cache", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task ProvisionAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_aspire_observability
                                           @value int,
                                           @secret nvarchar(128)
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           SELECT @value + 1 AS Value;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, $"""
                                        CREATE OR ALTER PROCEDURE dbo.caerius_aspire_observability_failure
                                        AS
                                        BEGIN
                                            SET NOCOUNT ON;
                                            THROW 51000, '{Secret}', 1;
                                        END
                                        """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_aspire_session
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           SELECT @@SPID AS Value;
                                       END
                                       """, cancellationToken);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string CreateSqlConnectionString(string source)
    {
        return new SqlConnectionStringBuilder(source)
        {
            InitialCatalog = "master",
            TrustServerCertificate = true
        }.ConnectionString;
    }

    private sealed class CollectingActivityProcessor : BaseProcessor<Activity>
    {
        private readonly ConcurrentQueue<ActivitySnapshot> _spans = [];

        public override void OnEnd(Activity data)
        {
            _spans.Enqueue(ActivitySnapshot.From(data));
        }

        public ActivitySnapshot[] Snapshot()
        {
            return _spans.ToArray();
        }

        public void Clear()
        {
            while (_spans.TryDequeue(out _))
            {
            }
        }
    }

    private sealed record ActivitySnapshot(
        string SourceName,
        ActivityTraceId TraceId,
        ActivitySpanId SpanId,
        ActivitySpanId ParentSpanId,
        ActivityStatusCode Status,
        string? StatusDescription,
        IReadOnlyDictionary<string, object?> Tags)
    {
        public static ActivitySnapshot From(Activity activity)
        {
            return new ActivitySnapshot(
                activity.Source.Name,
                activity.TraceId,
                activity.SpanId,
                activity.ParentSpanId,
                activity.Status,
                activity.StatusDescription,
                activity.TagObjects.ToDictionary(static tag => tag.Key, static tag => tag.Value,
                    StringComparer.Ordinal));
        }
    }

    private sealed class LeaseScopeTracker
    {
        private int _created;
        private int _disposed;

        public int Created => Volatile.Read(ref _created);
        public int Disposed => Volatile.Read(ref _disposed);

        public void OnCreated()
        {
            Interlocked.Increment(ref _created);
        }

        public void OnDisposed()
        {
            Interlocked.Increment(ref _disposed);
        }
    }

    private sealed class LeaseScopeProbe : IDisposable
    {
        private readonly LeaseScopeTracker _tracker;

        public LeaseScopeProbe(LeaseScopeTracker tracker)
        {
            _tracker = tracker;
            _tracker.OnCreated();
        }

        public void Dispose()
        {
            _tracker.OnDisposed();
        }
    }
}
