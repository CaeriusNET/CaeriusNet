using Aspire.Microsoft.Data.SqlClient;
using CaeriusNet.Abstractions;
using CaeriusNet;
using Microsoft.Extensions.DependencyInjection;
using CaeriusNet.Redis;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CaeriusNet.Aspire;

public static class CaeriusAspireExtensions
{
    public static IHostApplicationBuilder AddCaeriusNetAspire(
        this IHostApplicationBuilder builder,
        string sqlConnectionName,
        Action<CaeriusNetOptions>? configureCaerius = null,
        Action<CaeriusAspireOptions>? configureAspire = null,
        string? redisConnectionName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(sqlConnectionName);
        var aspireOptions = ConfigureOptions(builder, Options.DefaultName, configureAspire);

        AddSqlServerClientOnce(
            builder,
            sqlConnectionName,
            null,
            settings => settings.DisableTracing = !aspireOptions.EnableLowLevelSqlClientTracing);
        if (!string.IsNullOrWhiteSpace(redisConnectionName))
        {
            AddRedisClientOnce(builder, redisConnectionName);
            builder.Services.AddCaeriusNetRedis();
        }

        builder.Services.AddCaeriusNet(options =>
        {
            options.UseSqlServerLease((services, _) => CreateLeaseAsync(services, null));
            configureCaerius?.Invoke(options);
        });
        ConfigureTelemetry(builder);
        ConfigureHealthCheck(builder, aspireOptions, "default", null);
        return builder;
    }

    public static IHostApplicationBuilder AddKeyedCaeriusNetAspire(
        this IHostApplicationBuilder builder,
        string caeriusName,
        string sqlConnectionName,
        Action<CaeriusNetOptions>? configureCaerius = null,
        Action<CaeriusAspireOptions>? configureAspire = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(caeriusName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sqlConnectionName);
        var aspireOptions = ConfigureOptions(builder, caeriusName, configureAspire);

        AddSqlServerClientOnce(
            builder,
            sqlConnectionName,
            sqlConnectionName,
            settings => settings.DisableTracing = !aspireOptions.EnableLowLevelSqlClientTracing);
        builder.Services.AddCaeriusNet(caeriusName, options =>
        {
            options.UseSqlServerLease((services, _) => CreateLeaseAsync(services, sqlConnectionName));
            configureCaerius?.Invoke(options);
        });
        ConfigureTelemetry(builder);
        ConfigureHealthCheck(builder, aspireOptions, caeriusName, sqlConnectionName);
        return builder;
    }

    private static CaeriusAspireOptions ConfigureOptions(
        IHostApplicationBuilder builder,
        string optionsName,
        Action<CaeriusAspireOptions>? configure)
    {
        var options = new CaeriusAspireOptions();
        configure?.Invoke(options);
        var optionsBuilder = builder.Services.AddOptions<CaeriusAspireOptions>(optionsName);
        if (configure is not null)
            optionsBuilder.Configure(configure);
        return options;
    }

    private static async ValueTask<ICaeriusSqlConnectionLease> CreateLeaseAsync(
        IServiceProvider services,
        string? keyedName)
    {
        var scope = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        try
        {
            var connection = keyedName is null
                ? scope.ServiceProvider.GetRequiredService<SqlConnection>()
                : scope.ServiceProvider.GetRequiredKeyedService<SqlConnection>(keyedName);
            return new AspireConnectionLease(scope, connection);
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static void AddSqlServerClientOnce(
        IHostApplicationBuilder builder,
        string connectionName,
        string? serviceKey,
        Action<MicrosoftDataSqlClientSettings> configureSettings)
    {
        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(CaeriusAspireSqlClientRegistration) &&
                descriptor.ImplementationInstance is CaeriusAspireSqlClientRegistration registration &&
                string.Equals(registration.ConnectionName, connectionName, StringComparison.Ordinal) &&
                string.Equals(registration.ServiceKey, serviceKey, StringComparison.Ordinal)))
            return;

        if (serviceKey is null)
            builder.AddSqlServerClient(connectionName, configureSettings);
        else
            builder.AddKeyedSqlServerClient(connectionName, configureSettings);

        builder.Services.AddSingleton(new CaeriusAspireSqlClientRegistration(connectionName, serviceKey));
    }

    private static void AddRedisClientOnce(IHostApplicationBuilder builder, string connectionName)
    {
        // Aspire registers the multiplexer as an unkeyed singleton. A second registration would create a second
        // physical transport, so one process-wide client is the only meaningful idempotent outcome here.
        if (builder.Services.Any(static descriptor => descriptor.ServiceType == typeof(IConnectionMultiplexer)))
            return;

        builder.AddRedisClient(connectionName);
    }

    private static void ConfigureTelemetry(IHostApplicationBuilder builder)
    {
        if (builder.Services.Any(static descriptor =>
                descriptor.ServiceType == typeof(CaeriusAspireTelemetryRegistration)))
            return;

        builder.Services.AddSingleton<CaeriusAspireTelemetryRegistration>();
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(CaeriusTelemetry.ActivitySourceName))
            .WithMetrics(metrics => metrics
                .AddMeter(CaeriusTelemetry.MeterName)
                .AddMeter(CaeriusRedisTelemetry.MeterName));
    }

    private static void ConfigureHealthCheck(
        IHostApplicationBuilder builder,
        CaeriusAspireOptions options,
        string contextName,
        string? sqlClientKey)
    {
        if (!options.EnableSqlServerVersionHealthCheck)
            return;

        var timeout = options.SqlServerVersionHealthCheckTimeout;
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The SQL Server version health-check timeout must be greater than zero and no greater than 30 seconds.");

        var healthCheckName = $"caerius-sqlserver-version-{contextName}";
        if (builder.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(CaeriusSqlServerVersionCheckRegistration) &&
                descriptor.ImplementationInstance is CaeriusSqlServerVersionCheckRegistration registration &&
                string.Equals(registration.Name, healthCheckName, StringComparison.Ordinal)))
            return;

        builder.Services.AddSingleton(
            new CaeriusSqlServerVersionCheckRegistration(healthCheckName, sqlClientKey, timeout));
        builder.Services.TryAddSingleton<CaeriusSqlServerVersionHealthCheckRegistry>();
        builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            healthCheckName,
            provider => provider.GetRequiredService<CaeriusSqlServerVersionHealthCheckRegistry>()
                .GetHealthCheck(healthCheckName),
            null,
            ["caerius", "ready"]));
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, CaeriusVersionCheckHostedService>());
    }
}
