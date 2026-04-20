using CaeriusNet.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaeriusNet.Tests.Logging;

public sealed class LoggerProviderTests
{
    private static ILogger? Snapshot()
    {
        var method = typeof(LoggerProvider).GetMethod("GetLogger",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (ILogger?)method.Invoke(null, null);
    }

    [Fact]
    public void SetLogger_Then_GetLogger_Returns_Same_Instance()
    {
        var previous = Snapshot();
        try
        {
            var logger = NullLogger.Instance;
            LoggerProvider.SetLogger(logger);
            Assert.Same(logger, Snapshot());
        }
        finally
        {
            if (previous is not null) LoggerProvider.SetLogger(previous);
        }
    }

    [Fact]
    public void SetLogger_Replaces_Previous_Instance()
    {
        var previous = Snapshot();
        try
        {
            var first = NullLoggerFactory.Instance.CreateLogger("first");
            var second = NullLoggerFactory.Instance.CreateLogger("second");
            LoggerProvider.SetLogger(first);
            Assert.Same(first, Snapshot());
            LoggerProvider.SetLogger(second);
            Assert.Same(second, Snapshot());
        }
        finally
        {
            if (previous is not null) LoggerProvider.SetLogger(previous);
        }
    }
}
