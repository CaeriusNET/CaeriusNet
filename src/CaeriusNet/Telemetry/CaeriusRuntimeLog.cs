namespace CaeriusNet.Telemetry;

/// <summary>
///     Source-generated runtime diagnostics.  The messages intentionally accept only contract-safe fields: callers
///     must never attach a transport exception because provider messages can contain cache keys or payload fragments.
/// </summary>
internal static partial class CaeriusRuntimeLog
{
    [LoggerMessage(
        EventId = 1,
        EventName = "CaeriusRedisLookupFailed",
        Level = LogLevel.Warning,
        Message = "Caerius Redis lookup failed for {Procedure} ({ErrorType}); falling back to SQL.")]
    internal static partial void RedisLookupFailed(ILogger logger, string procedure, string errorType);

    [LoggerMessage(
        EventId = 2,
        EventName = "CaeriusRedisPopulationFailed",
        Level = LogLevel.Warning,
        Message = "Caerius Redis population failed for {Procedure} ({ErrorType}); the SQL result is returned.")]
    internal static partial void RedisPopulationFailed(ILogger logger, string procedure, string errorType);
}
