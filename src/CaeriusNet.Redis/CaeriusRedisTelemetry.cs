namespace CaeriusNet.Redis;

/// <summary>
///     Names the Redis-specific meter.  The meter remains private to the integration; this constant lets hosts opt in
///     without creating a second meter with the SQL runtime's identity.
/// </summary>
public static class CaeriusRedisTelemetry
{
    public const string MeterName = "CaeriusNet.Redis";
}
