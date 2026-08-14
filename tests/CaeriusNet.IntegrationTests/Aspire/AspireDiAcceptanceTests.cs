using CaeriusNet.Aspire;
using CaeriusNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CaeriusNet.IntegrationTests.Aspire;

/// <summary>
///     Exercises the Aspire composition root without opening a network connection. The Aspire client
///     integrations are deliberately lazy: resolving a context proves the scoped/keyed wiring while a command
///     execution remains the responsibility of the SQL Server container acceptance matrix.
/// </summary>
public sealed class AspireDiAcceptanceTests
{
    [Fact]
    public async Task DefaultRegistrationWiresSqlContextAndOneCachedHealthCheck()
    {
        var builder = CreateBuilder("default-sql");
        builder.AddCaeriusNetAspire(
            "default-sql",
            options => options.ApplicationNamespace = "host-default",
            options => options.EnableSqlServerVersionHealthCheck = true);

        await using var provider = builder.Services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        Assert.Equal(Options.DefaultName, context.Name);
        Assert.Equal("host-default",
            provider.GetRequiredService<IOptionsMonitor<CaeriusNetOptions>>().CurrentValue.ApplicationNamespace);

        // This descriptor and the connection below verify that the real Aspire extension was used. Resolving a
        // SqlConnection constructs it from configuration but does not open a network connection.
        Assert.Contains(builder.Services, static descriptor =>
            descriptor.ServiceType == typeof(SqlConnection) && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Equal(
            "Server=localhost,1433;Database=default-sql;User ID=sa;Password=Caerius!Test_2026;TrustServerCertificate=True",
            scope.ServiceProvider.GetRequiredService<SqlConnection>().ConnectionString);

        var healthOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var registration = Assert.Single(healthOptions.Registrations, static candidate =>
            candidate.Name == "caerius-sqlserver-version-default");
        Assert.Contains("caerius", registration.Tags);
        Assert.Contains("ready", registration.Tags);

        // The registry, rather than a transient HealthCheckRegistration factory, owns this instance. Consequently
        // the SQL Server version probe and its result cache are shared by all health-check invocations.
        Assert.Same(registration.Factory(provider), registration.Factory(provider));
    }

    [Fact]
    public void RedisAspireClientIsRegisteredWithoutEagerlyOpeningItsTransport()
    {
        var builder = CreateBuilder("default-sql", "cache");
        builder.AddCaeriusNetAspire("default-sql", redisConnectionName: "cache");

        // AddRedisClient owns an IConnectionMultiplexer singleton. This only inspects DI metadata: resolving the
        // singleton would be an integration test of the Redis transport and is deliberately covered by the
        // Testcontainers acceptance suite instead.
        Assert.Contains(builder.Services, static descriptor =>
            descriptor.ServiceType == typeof(IConnectionMultiplexer) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(builder.Services, static descriptor =>
            descriptor.ServiceType == typeof(IRedisCacheManager) && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public async Task KeyedContextsUseIndependentOptionsAndKeepOneCaeriusInstrumentationPerHost()
    {
        var builder = CreateBuilder("orders-sql", "reporting-sql");
        builder.AddKeyedCaeriusNetAspire(
            "orders",
            "orders-sql",
            options => options.ApplicationNamespace = "orders-host",
            options =>
            {
                options.EnableSqlServerVersionHealthCheck = true;
                options.EnableLowLevelSqlClientTracing = true;
            });
        builder.AddKeyedCaeriusNetAspire(
            "reporting",
            "reporting-sql",
            options => options.ApplicationNamespace = "reporting-host",
            options => options.EnableSqlServerVersionHealthCheck = true);

        // Caerius instrumentation is owned by DI and shared in a host. There cannot be one Caerius ActivitySource
        // per keyed context, which prevents duplicate Caerius spans for one executed command.
        Assert.Single(builder.Services, static descriptor =>
            descriptor.ServiceType.FullName == "CaeriusNet.CaeriusInstrumentation" &&
            descriptor.Lifetime == ServiceLifetime.Singleton);

        await using var provider = builder.Services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var orders = scope.ServiceProvider.GetRequiredKeyedService<ICaeriusNetDbContext>("orders");
        var reporting = scope.ServiceProvider.GetRequiredKeyedService<ICaeriusNetDbContext>("reporting");
        Assert.Equal("orders", orders.Name);
        Assert.Equal("reporting", reporting.Name);

        var caeriusOptions = provider.GetRequiredService<IOptionsMonitor<CaeriusNetOptions>>();
        Assert.Equal("orders-host", caeriusOptions.Get("orders").ApplicationNamespace);
        Assert.Equal("reporting-host", caeriusOptions.Get("reporting").ApplicationNamespace);

        var aspireOptions = provider.GetRequiredService<IOptionsMonitor<CaeriusAspireOptions>>();
        Assert.True(aspireOptions.Get("orders").EnableLowLevelSqlClientTracing);
        Assert.False(aspireOptions.Get("reporting").EnableLowLevelSqlClientTracing);

        // Aspire captures the configured settings in its scoped connection factories. The opt-in obtains low-level
        // SqlClient tracing for orders while the reporting context keeps it disabled (the Caerius span remains).
        Assert.Equal(
            "Server=localhost,1433;Database=orders-sql;User ID=sa;Password=Caerius!Test_2026;TrustServerCertificate=True",
            scope.ServiceProvider.GetRequiredKeyedService<SqlConnection>("orders-sql").ConnectionString);
        Assert.Equal(
            "Server=localhost,1433;Database=reporting-sql;User ID=sa;Password=Caerius!Test_2026;TrustServerCertificate=True",
            scope.ServiceProvider.GetRequiredKeyedService<SqlConnection>("reporting-sql").ConnectionString);

        var healthOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Contains(healthOptions.Registrations, static registration =>
            registration.Name == "caerius-sqlserver-version-orders");
        Assert.Contains(healthOptions.Registrations, static registration =>
            registration.Name == "caerius-sqlserver-version-reporting");
        Assert.Single(builder.Services, static descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType?.FullName == "CaeriusNet.Aspire.CaeriusVersionCheckHostedService");
    }

    [Fact]
    public void RepeatingTheSameAspireRegistrationDoesNotDuplicateSqlOrTelemetryServices()
    {
        var builder = CreateBuilder("orders-sql");
        for (var call = 0; call < 2; call++)
            builder.AddKeyedCaeriusNetAspire(
                "orders",
                "orders-sql",
                configureAspire: options => options.EnableSqlServerVersionHealthCheck = true);

        Assert.Single(builder.Services, static descriptor =>
            descriptor.ServiceType == typeof(SqlConnection) &&
            string.Equals(descriptor.ServiceKey as string, "orders-sql", StringComparison.Ordinal));
        Assert.Single(builder.Services, static descriptor =>
            descriptor.ServiceType.FullName == "CaeriusNet.CaeriusInstrumentation");
        Assert.Single(builder.Services, static descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType?.FullName == "CaeriusNet.Aspire.CaeriusVersionCheckHostedService");
        Assert.Single(builder.Services, static descriptor =>
            descriptor.ServiceType == typeof(ICaeriusNetDbContext) &&
            string.Equals(descriptor.ServiceKey as string, "orders", StringComparison.Ordinal));

        using var provider = builder.Services.BuildServiceProvider();
        var healthOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Single(healthOptions.Registrations, static registration =>
            registration.Name == "caerius-sqlserver-version-orders");
    }

    [Fact]
    public async Task SeparateHostsKeepCacheManagersAndCacheNamespacesIsolated()
    {
        var firstBuilder = CreateBuilder("first-sql");
        firstBuilder.AddCaeriusNetAspire(
            "first-sql",
            options => options.ApplicationNamespace = "first-app");
        var secondBuilder = CreateBuilder("second-sql");
        secondBuilder.AddCaeriusNetAspire(
            "second-sql",
            options => options.ApplicationNamespace = "second-app");

        await using var firstProvider = firstBuilder.Services.BuildServiceProvider();
        await using var secondProvider = secondBuilder.Services.BuildServiceProvider();
        await using var firstScope = firstProvider.CreateAsyncScope();
        await using var secondScope = secondProvider.CreateAsyncScope();

        var firstContext = firstScope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        Assert.NotSame(
            firstProvider.GetRequiredService<IInMemoryCacheManager>(),
            secondProvider.GetRequiredService<IInMemoryCacheManager>());
        Assert.NotSame(
            firstProvider.GetRequiredService<IFrozenCacheManager>(),
            secondProvider.GetRequiredService<IFrozenCacheManager>());

        var command = new StoredProcedureCommandBuilder("dbo", "get_widget")
            .AddParameter("@id", 7, SqlDbType.Int)
            .Build();
        var firstOptions = firstProvider.GetRequiredService<IOptionsMonitor<CaeriusNetOptions>>().CurrentValue;
        var secondOptions = secondProvider.GetRequiredService<IOptionsMonitor<CaeriusNetOptions>>().CurrentValue;
        var firstKey = CaeriusCacheKeyBuilder.Create(firstOptions.ApplicationNamespace, firstContext.Name, command,
            "query:Widget[]");
        var secondKey = CaeriusCacheKeyBuilder.Create(secondOptions.ApplicationNamespace, secondContext.Name, command,
            "query:Widget[]");
        Assert.NotEqual(firstKey, secondKey);
    }

    private static HostApplicationBuilder CreateBuilder(params string[] sqlConnectionNames)
    {
        var configuration = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var name in sqlConnectionNames)
            configuration[$"ConnectionStrings:{name}"] =
                $"Server=localhost,1433;Database={name};User ID=sa;Password=Caerius!Test_2026;TrustServerCertificate=True";
        configuration["ConnectionStrings:cache"] = "localhost:6379,abortConnect=false";

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(configuration);
        return builder;
    }
}
