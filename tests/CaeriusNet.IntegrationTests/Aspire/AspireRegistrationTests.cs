using CaeriusNet.Aspire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CaeriusNet.IntegrationTests.Aspire;

public sealed class AspireRegistrationTests
{
    [Fact]
    public void KeyedContextsHaveDistinctNamedOptionsAndHealthChecks()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:orders"] = "Server=(local);Database=orders;Integrated Security=true",
            ["ConnectionStrings:reporting"] = "Server=(local);Database=reporting;Integrated Security=true"
        });

        builder.AddKeyedCaeriusNetAspire(
            "orders-context",
            "orders",
            configureAspire: options => options.EnableSqlServerVersionHealthCheck = true);
        builder.AddKeyedCaeriusNetAspire(
            "reporting-context",
            "reporting",
            configureAspire: options => options.EnableSqlServerVersionHealthCheck = true);

        using var provider = builder.Services.BuildServiceProvider();
        var aspireOptions = provider.GetRequiredService<IOptionsMonitor<CaeriusAspireOptions>>();
        Assert.True(aspireOptions.Get("orders-context").EnableSqlServerVersionHealthCheck);
        Assert.True(aspireOptions.Get("reporting-context").EnableSqlServerVersionHealthCheck);

        var healthOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Contains(healthOptions.Registrations, static registration =>
            registration.Name == "caerius-sqlserver-version-orders-context");
        Assert.Contains(healthOptions.Registrations, static registration =>
            registration.Name == "caerius-sqlserver-version-reporting-context");
    }
}
