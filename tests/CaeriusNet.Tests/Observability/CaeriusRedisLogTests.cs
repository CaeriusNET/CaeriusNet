using CaeriusNet.Redis;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CaeriusNet.Tests.Observability;

public sealed class CaeriusRedisLogTests
{
    [Fact]
    public void RedisCorruptionLogIsSourceGeneratedAndDoesNotAttachAnException()
    {
        var logger = new CapturingLogger();

        CaeriusRedisLog.CorruptPayload(logger, typeof(InvalidDataException).FullName!);

        Assert.Null(logger.Exception);
        Assert.Equal(typeof(InvalidDataException).FullName, logger.State["ErrorType"]);
        Assert.Equal("CaeriusNet.Redis", CaeriusRedisTelemetry.MeterName);
        Assert.DoesNotContain(logger.State.Keys, static key => key.Contains("key", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logger.State.Keys,
            static key => key.Contains("payload", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CapturingLogger : ILogger
    {
        public Exception? Exception { get; private set; }
        public Dictionary<string, object?> State { get; } = new(StringComparer.Ordinal);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Exception = exception;
            if (state is IEnumerable<KeyValuePair<string, object?>> properties)
                foreach (var property in properties)
                    State[property.Key] = property.Value;
        }
    }
}
