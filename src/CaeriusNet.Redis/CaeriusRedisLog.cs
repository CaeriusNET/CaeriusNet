using Microsoft.Extensions.Logging;

namespace CaeriusNet.Redis;

internal static partial class CaeriusRedisLog
{
    [LoggerMessage(
        EventId = 12001,
        EventName = "CorruptRedisPayload",
        Level = LogLevel.Warning,
        Message = "Discarding an incompatible or corrupt Caerius Redis payload ({ErrorType}).")]
    internal static partial void CorruptPayload(ILogger logger, string errorType);

    [LoggerMessage(
        EventId = 12002,
        EventName = "CorruptRedisRemovalFailed",
        Level = LogLevel.Debug,
        Message = "Best-effort removal of a corrupt Caerius Redis payload failed ({ErrorType}).")]
    internal static partial void CorruptRemovalFailed(ILogger logger, string errorType);
}
