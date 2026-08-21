using Microsoft.Extensions.Logging;

namespace CaeriusNet.Aspire;

internal static partial class CaeriusAspireLog
{
    [LoggerMessage(
        EventId = 13001,
        EventName = "SqlServerVersionCheckFailed",
        Level = LogLevel.Warning,
        Message = "SQL Server version validation failed ({ErrorType}).")]
    internal static partial void SqlServerVersionCheckFailed(ILogger logger, string errorType);
}
