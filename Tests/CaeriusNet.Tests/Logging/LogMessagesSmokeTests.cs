using CaeriusNet.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaeriusNet.Tests.Logging;

/// <summary>
///     Smoke-tests every <see cref="LogMessages" /> partial method against a NullLogger to ensure the
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
        Logger.LogStoringInMemoryCache("k");
        Logger.LogStoredInMemoryCache("k", TimeSpan.FromSeconds(30));
        Logger.LogRetrievingFromMemoryCache("k");
        Logger.LogRetrievedFromMemoryCache("k");
        Logger.LogNotFoundInMemoryCache("k");

        Logger.LogStoringInFrozenCache("k");
        Logger.LogFrozenCacheKeyExists("k");
        Logger.LogStoredInFrozenCache("k");
        Logger.LogRetrievingFromFrozenCache("k");
        Logger.LogRetrievedFromFrozenCache("k");
        Logger.LogNotFoundInFrozenCache("k");
        Logger.LogStoredRangeInFrozenCache(5);
    }

    [Fact]
    public void Database_And_Sproc_Methods_DoNotThrow()
    {
        Logger.LogDatabaseConnecting();
        Logger.LogDatabaseConnected();
        Logger.LogDatabaseConnectionFailed(new InvalidOperationException("nope"));

        Logger.LogExecutingStoredProcedure("dbo.sp_x", 3);
        Logger.LogStoredProcedureExecuted("dbo.sp_x", 12L);
        Logger.LogStoredProcedureExecutionFailed("dbo.sp_x",
            new InvalidOperationException("boom"));

        Logger.LogReadingResultSets("dbo.sp_x", 2);
        Logger.LogResultSetRead("dbo.sp_x", 7);
    }

    [Fact]
    public void Transaction_Methods_DoNotThrow()
    {
        Logger.LogTransactionStarted(IsolationLevel.ReadCommitted);
        Logger.LogTransactionCommitted();
        Logger.LogTransactionRolledBack();
    }

    [Fact]
    public void Transaction_Poison_Method_DoesNotThrow()
    {
        Logger.LogTransactionPoisoned();
    }

    [Fact]
    public void Command_Execution_Methods_DoNotThrow()
    {
        Logger.LogExecutingProcedure("dbo", "sp_Test", 3);
        Logger.LogProcedureCompleted("dbo", "sp_Test", 42L, 10);
        Logger.LogProcedureScalarCompleted("dbo", "sp_Test", 5L);
        Logger.LogProcedureNonQueryCompleted("dbo", "sp_Test", 8L, 3);
        Logger.LogCacheHitSkippingExecution("test_cache_key");
    }
}