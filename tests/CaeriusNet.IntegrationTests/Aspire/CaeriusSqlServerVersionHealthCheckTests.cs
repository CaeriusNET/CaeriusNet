using System.Diagnostics;
using CaeriusNet.Aspire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CaeriusNet.IntegrationTests.Aspire;

public sealed class CaeriusSqlServerVersionHealthCheckTests
{
    [Fact]
    public async Task FailedVersionProbeIsBoundedCachedAndDoesNotExposeItsProviderException()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // There is deliberately no SQL Server listener on this loopback port. The health-check token, rather
            // than SqlClient's much longer connection timeout, must control the one-time readiness probe.
            ["ConnectionStrings:unreachable"] =
                "Server=127.0.0.1,1;Database=unreachable;Connect Timeout=30;TrustServerCertificate=True"
        });
        builder.AddCaeriusNetAspire(
            "unreachable",
            configureAspire: options =>
            {
                options.EnableSqlServerVersionHealthCheck = true;
                options.SqlServerVersionHealthCheckTimeout = TimeSpan.FromSeconds(1);
            });

        await using var provider = builder.Services.BuildServiceProvider();
        var registration = Assert.Single(
            provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations,
            static candidate => candidate.Name == "caerius-sqlserver-version-default");
        var check = registration.Factory(provider);
        var stopwatch = Stopwatch.StartNew();
        var first = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
        stopwatch.Stop();
        var second = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, first.Status);
        Assert.Null(first.Exception);
        Assert.Equal(first.Description, second.Description);
        Assert.Null(second.Exception);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(4),
            $"The bounded startup health check took {stopwatch.Elapsed} instead of respecting its one-second timeout.");
    }

    [Fact]
    public async Task ArbitraryNonFatalProbeFailureIsUnhealthyAndDoesNotExposeItsException()
    {
        var builder = CreateBuilder();
        builder.AddCaeriusNetAspire(
            "sql",
            configureAspire: options => options.EnableSqlServerVersionHealthCheck = true);
        builder.Services.RemoveAll<SqlConnection>();
        builder.Services.AddScoped<SqlConnection>(static _ =>
            throw new InvalidOperationException("The test connection factory failed before SQL I/O."));

        await using var provider = builder.Services.BuildServiceProvider();
        var result = await GetVersionCheck(provider)
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("SQL Server version validation failed.", result.Description);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task CallerCancellationIsPropagatedWithoutBeingReportedAsUnhealthy()
    {
        var builder = CreateBuilder();
        builder.AddCaeriusNetAspire(
            "sql",
            configureAspire: options => options.EnableSqlServerVersionHealthCheck = true);
        await using var provider = builder.Services.BuildServiceProvider();
        var check = GetVersionCheck(provider);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check.CheckHealthAsync(
            new HealthCheckContext(), cancellationSource.Token));
    }

    [Fact]
    public async Task FatalProbeFailureIsPropagated()
    {
        var builder = CreateBuilder();
        builder.AddCaeriusNetAspire(
            "sql",
            configureAspire: options => options.EnableSqlServerVersionHealthCheck = true);
        builder.Services.RemoveAll<SqlConnection>();
        builder.Services.AddScoped<SqlConnection>(static _ =>
            throw new BadImageFormatException("The test connection factory raised a fatal exception."));

        await using var provider = builder.Services.BuildServiceProvider();
        await Assert.ThrowsAsync<BadImageFormatException>(() => GetVersionCheck(provider).CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void VersionProbeTimeoutMustRemainBounded()
    {
        var builder = CreateBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddCaeriusNetAspire(
            "sql",
            configureAspire: options =>
            {
                options.EnableSqlServerVersionHealthCheck = true;
                options.SqlServerVersionHealthCheckTimeout = TimeSpan.FromSeconds(31);
            }));
    }

    private static HostApplicationBuilder CreateBuilder()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:sql"] = "Server=localhost;Database=caerius;TrustServerCertificate=True"
        });
        return builder;
    }

    private static IHealthCheck GetVersionCheck(ServiceProvider provider)
    {
        var registration = Assert.Single(
            provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations,
            static candidate => candidate.Name == "caerius-sqlserver-version-default");
        return registration.Factory(provider);
    }
}
