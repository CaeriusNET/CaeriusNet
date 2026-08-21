using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace CaeriusNet.Aspire;

internal sealed class CaeriusSqlServerVersionHealthCheck(
    IServiceScopeFactory scopeFactory,
    string? keyedName,
    TimeSpan timeout,
    ILogger<CaeriusSqlServerVersionHealthCheck> logger) : IHealthCheck, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HealthCheckResult? _cached;

    public void Dispose()
    {
        _gate.Dispose();
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_cached is { } cached)
            return cached;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cached is { } secondCached)
                return secondCached;
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            var timeoutToken = timeoutSource.Token;
            await using var scope = scopeFactory.CreateAsyncScope();
            var connection = keyedName is null
                ? scope.ServiceProvider.GetRequiredService<SqlConnection>()
                : scope.ServiceProvider.GetRequiredKeyedService<SqlConnection>(keyedName);
            if (connection.State == ConnectionState.Closed)
                await connection.OpenAsync(timeoutToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CONVERT(int, SERVERPROPERTY('ProductMajorVersion'))";
            command.CommandType = CommandType.Text;
            command.CommandTimeout = Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds));
            var version = (int)(await command.ExecuteScalarAsync(timeoutToken).ConfigureAwait(false)
                                ?? throw new InvalidOperationException("SQL Server returned no product version."));
            _cached = version is 16 or 17
                ? HealthCheckResult.Healthy($"SQL Server major version {version} is supported.")
                : HealthCheckResult.Unhealthy($"SQL Server major version {version} is unsupported; expected 16 or 17.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            CaeriusAspireLog.SqlServerVersionCheckFailed(logger, typeof(TimeoutException).FullName!);
            _cached = HealthCheckResult.Unhealthy("SQL Server version validation timed out.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsNonFatal(exception))
        {
            CaeriusAspireLog.SqlServerVersionCheckFailed(logger,
                exception.GetType().FullName ?? exception.GetType().Name);
            _cached = HealthCheckResult.Unhealthy("SQL Server version validation failed.");
        }
        finally
        {
            _gate.Release();
        }

        return _cached.Value;
    }

    private static bool IsNonFatal(Exception exception)
    {
        return exception is not OutOfMemoryException
            and not StackOverflowException
            and not AccessViolationException
            and not AppDomainUnloadedException
            and not BadImageFormatException
            and not CannotUnloadAppDomainException
            and not System.Threading.ThreadAbortException;
    }
}
