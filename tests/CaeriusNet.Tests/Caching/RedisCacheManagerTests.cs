using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CaeriusNet.Abstractions;
using CaeriusNet.Exceptions;
using CaeriusNet.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace CaeriusNet.Tests.Caching;

public sealed class RedisCacheManagerTests
{
    [Fact]
    public async Task TryGetAsyncWrapsRedisProtocolFailures()
    {
        var redisFailure = new RedisException("Redis connection failed.");
        using var manager = CreateManager(redisFailure);

        var exception = await Assert.ThrowsAsync<CaeriusCacheException>(() =>
            manager.TryGetAsync("cache-key", TestCodec.Instance, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("get", exception.Operation);
        Assert.Same(redisFailure, exception.InnerException);
    }

    [Fact]
    public async Task TryGetAsyncWrapsRedisTimeoutFailures()
    {
        var redisFailure = new RedisTimeoutException(
            CommandFlags.None,
            "Redis operation timed out.",
            CommandStatus.Unknown);
        using var manager = CreateManager(redisFailure);

        var exception = await Assert.ThrowsAsync<CaeriusCacheException>(() =>
            manager.TryGetAsync("cache-key", TestCodec.Instance, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("get", exception.Operation);
        Assert.Same(redisFailure, exception.InnerException);
    }

    [Fact]
    public async Task TryGetAsyncPreservesAnUnrelatedTimeoutFailure()
    {
        var timeout = new TimeoutException("An unrelated task timed out.");
        using var manager = CreateManager(timeout);

        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            manager.TryGetAsync("cache-key", TestCodec.Instance, TestContext.Current.CancellationToken).AsTask());

        Assert.Same(timeout, exception);
    }

    private static RedisCacheManager CreateManager(Exception failure)
    {
        var database = DispatchProxy.Create<IDatabase, FaultingDatabaseProxy>();
        ((FaultingDatabaseProxy)(object)database).Failure = failure;
        var multiplexer = DispatchProxy.Create<IConnectionMultiplexer, SingleDatabaseMultiplexerProxy>();
        ((SingleDatabaseMultiplexerProxy)(object)multiplexer).Database = database;
        return new RedisCacheManager(
            multiplexer,
            Options.Create(new CaeriusRedisOptions { KeyPrefix = "tests:" }),
            NullLogger<RedisCacheManager>.Instance);
    }

    [SuppressMessage(
        "Performance",
        "CA1852:Seal internal types",
        Justification = "DispatchProxy creates a runtime-derived proxy type.")]
    private class FaultingDatabaseProxy : DispatchProxy
    {
        internal Exception Failure { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? arguments)
        {
            if (targetMethod?.Name == nameof(IDatabaseAsync.StringGetLeaseAsync))
                return Task.FromException<Lease<byte>?>(Failure);

            throw new NotSupportedException($"Unexpected Redis database call: {targetMethod?.Name}.");
        }
    }

    [SuppressMessage(
        "Performance",
        "CA1852:Seal internal types",
        Justification = "DispatchProxy creates a runtime-derived proxy type.")]
    private class SingleDatabaseMultiplexerProxy : DispatchProxy
    {
        internal IDatabase Database { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? arguments)
        {
            if (targetMethod?.Name == nameof(IConnectionMultiplexer.GetDatabase))
                return Database;

            throw new NotSupportedException($"Unexpected Redis multiplexer call: {targetMethod?.Name}.");
        }
    }

    private sealed class TestCodec : ICaeriusCacheCodec<int>
    {
        internal static TestCodec Instance { get; } = new();

        public string CodecId => "tests/int32/v1";
        public string ContractHash => "3ba1d467";

        public void Encode(IBufferWriter<byte> writer, int value)
        {
            throw new NotSupportedException();
        }

        public int Decode(ReadOnlySpan<byte> payload)
        {
            throw new NotSupportedException();
        }
    }
}
