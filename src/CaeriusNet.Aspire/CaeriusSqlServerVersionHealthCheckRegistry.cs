using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace CaeriusNet.Aspire;

/// <summary>
///     Owns one cached check per Caerius context. HealthCheckService may invoke its registration
///     factory more than once, so it must not be the lifetime owner of the cached SQL version result.
/// </summary>
internal sealed class CaeriusSqlServerVersionHealthCheckRegistry : IDisposable
{
    private readonly ConcurrentDictionary<string, CaeriusSqlServerVersionHealthCheck> _checks =
        new(StringComparer.Ordinal);

    private readonly ILogger<CaeriusSqlServerVersionHealthCheck> _logger;
    private readonly IReadOnlyDictionary<string, CaeriusSqlServerVersionCheckRegistration> _registrations;
    private readonly IServiceScopeFactory _scopeFactory;

    public CaeriusSqlServerVersionHealthCheckRegistry(
        IServiceScopeFactory scopeFactory,
        IEnumerable<CaeriusSqlServerVersionCheckRegistration> registrations,
        ILogger<CaeriusSqlServerVersionHealthCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _registrations = registrations.ToDictionary(
            static registration => registration.Name,
            static registration => registration,
            StringComparer.Ordinal);
    }

    public void Dispose()
    {
        foreach (var healthCheck in _checks.Values)
            healthCheck.Dispose();
    }

    public CaeriusSqlServerVersionHealthCheck GetHealthCheck(string name)
    {
        if (!_registrations.TryGetValue(name, out var registration))
            throw new InvalidOperationException($"The Caerius SQL Server health check '{name}' was not registered.");

        return _checks.GetOrAdd(name, _ => new CaeriusSqlServerVersionHealthCheck(
            _scopeFactory,
            registration.SqlClientKey,
            registration.Timeout,
            _logger));
    }

    public async Task CheckAllAsync(CancellationToken cancellationToken)
    {
        foreach (var name in _registrations.Keys)
            _ = await GetHealthCheck(name)
                .CheckHealthAsync(new HealthCheckContext(), cancellationToken)
                .ConfigureAwait(false);
    }
}