using CaeriusNet.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaeriusNet.Tests.Logging;

/// <summary>
///     Unit tests for <see cref="LoggerProvider" />.
///     These tests mutate process-wide static state; each test snapshots and unconditionally
///     restores the previous value (including <c>null</c>) via reflection so no state leaks
///     to subsequent tests.
/// </summary>
public sealed class LoggerProviderTests
{
    // Backing field on LoggerProvider — kept as a cached FieldInfo so reflection cost is paid once.
    private static readonly FieldInfo LoggerField =
        typeof(LoggerProvider).GetField("_logger",
            BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Reads the current static logger value (may be <c>null</c>).</summary>
    private static ILogger? Snapshot()
    {
        return (ILogger?)LoggerField.GetValue(null);
    }

    /// <summary>Unconditionally writes <paramref name="value" /> back, even when <c>null</c>.</summary>
    private static void Restore(ILogger? value)
    {
        LoggerField.SetValue(null, value);
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
            Restore(previous);
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
            Restore(previous);
        }
    }

    [Fact]
    public void GetLogger_WithoutSetLogger_Returns_Null()
    {
        var previous = Snapshot();
        try
        {
            Restore(null);
            Assert.Null(LoggerProvider.GetLogger());
        }
        finally
        {
            Restore(previous);
        }
    }

    [Fact]
    public void GetLogger_After_SetLogger_Returns_NonNull()
    {
        var previous = Snapshot();
        try
        {
            var logger = NullLogger.Instance;
            LoggerProvider.SetLogger(logger);
            Assert.NotNull(LoggerProvider.GetLogger());
        }
        finally
        {
            Restore(previous);
        }
    }
}
