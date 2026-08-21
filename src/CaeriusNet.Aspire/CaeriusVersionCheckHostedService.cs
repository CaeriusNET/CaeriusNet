using Microsoft.Extensions.Hosting;

namespace CaeriusNet.Aspire;

internal sealed class CaeriusVersionCheckHostedService(
    CaeriusSqlServerVersionHealthCheckRegistry registry) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return registry.CheckAllAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
