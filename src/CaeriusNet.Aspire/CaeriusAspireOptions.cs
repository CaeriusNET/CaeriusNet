namespace CaeriusNet.Aspire;

public sealed class CaeriusAspireOptions
{
    public bool EnableLowLevelSqlClientTracing { get; set; }
    public bool EnableSqlServerVersionHealthCheck { get; set; }

    /// <summary>
    ///     Maximum duration of the one-time SQL Server major-version probe performed at host startup.
    ///     The value is deliberately bounded by the Aspire integration because it must never become a long-running
    ///     health-check path.
    /// </summary>
    public TimeSpan SqlServerVersionHealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
