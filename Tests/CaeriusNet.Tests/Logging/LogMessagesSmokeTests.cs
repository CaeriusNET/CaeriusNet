using CaeriusNet.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaeriusNet.Tests.Logging;

/// <summary>
///     Smoke-tests every <see cref="LogMessages"/> partial method against a NullLogger to ensure the
///     <c>LoggerMessageGenerator</c>-emitted code does not throw on call. We deliberately don't assert
///     content — these methods are pure delegating wrappers around the generator output, and the format
///     strings are themselves validated at compile time.
/// </summary>
public sealed class LogMessagesSmokeTests
{
    private static readonly NullLogger Logger = NullLogger.Instance;

    [Fact]
    public void Cache_Methods_DoNotThrow()
    {
        LogMessages.LogStoringInMemoryCache(Logger, "k");
        LogMessages.LogStoredInMemoryCache(Logger, "k", TimeSpan.FromSeconds(30));
        LogMessages.LogRetrievingFromMemoryCache(Logger, "k");
        LogMessages.LogRetrievedFromMemoryCache(Logger, "k");
        LogMessages.LogNotFoundInMemoryCache(Logger, "k");

        LogMessages.LogStoringInFrozenCache(Logger, "k");
        LogMessages.LogFrozenCacheKeyExists(Logger, "k");
        LogMessages.LogStoredInFrozenCache(Logger, "k");
        LogMessages.LogRetrievingFromFrozenCache(Logger, "k");
        LogMessages.LogRetrievedFromFrozenCache(Logger, "k");
        LogMessages.LogNotFoundInFrozenCache(Logger, "k");
        LogMessages.LogStoredRangeInFrozenCache(Logger, 5);
    }

    [Fact]
    public void Database_And_Sproc_Methods_DoNotThrow()
    {
        LogMessages.LogDatabaseConnecting(Logger);
        LogMessages.LogDatabaseConnected(Logger);
        LogMessages.LogDatabaseConnectionFailed(Logger, new InvalidOperationException("nope"));

        LogMessages.LogExecutingStoredProcedure(Logger, "dbo.sp_x", 3);
        LogMessages.LogStoredProcedureExecuted(Logger, "dbo.sp_x", 12L);
        LogMessages.LogStoredProcedureExecutionFailed(Logger, "dbo.sp_x",
            new InvalidOperationException("boom"));

        LogMessages.LogReadingResultSets(Logger, "dbo.sp_x", 2);
        LogMessages.LogResultSetRead(Logger, "dbo.sp_x", 7);
    }

    [Fact]
    public void Transaction_Methods_DoNotThrow()
    {
        LogMessages.LogTransactionStarted(Logger, System.Data.IsolationLevel.ReadCommitted);
        LogMessages.LogTransactionCommitted(Logger);
        LogMessages.LogTransactionRolledBack(Logger);
    }
}
