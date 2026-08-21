using System.Buffers;
using System.Collections.Concurrent;
using CaeriusNet.Abstractions;
using CaeriusNet.Caching;
using CaeriusNet.Commands;
using CaeriusNet.Exceptions;
using CaeriusNet.Execution;
using CaeriusNet.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Xunit;

namespace CaeriusNet.Tests.Caching;

/// <summary>
///     End-to-end acceptance tests for the three cache contracts.  They deliberately use the
///     public managers and a deterministic remote-cache double: SQL is not part of these tests.
/// </summary>
public sealed class CacheAcceptanceTests
{
    [Fact]
    public void InMemoryCacheHonorsAbsoluteAndSlidingTtl()
    {
        var clock = new ManualSystemClock(DateTimeOffset.UnixEpoch);
        using var cache = new InMemoryCacheManager(
            Options.Create(new CaeriusMemoryCacheOptions { SizeLimit = 32 }),
            clock);

        cache.SetEntry("absolute", 1, TimeSpan.FromMilliseconds(80));
        clock.Advance(TimeSpan.FromMilliseconds(80));
        Assert.False(cache.TryGet<int>("absolute", out _));

        cache.SetEntry("sliding", 2, TimeSpan.FromMilliseconds(120), true);
        clock.Advance(TimeSpan.FromMilliseconds(70));
        Assert.True(cache.TryGet<int>("sliding", out var first));
        Assert.Equal(2, first);
        clock.Advance(TimeSpan.FromMilliseconds(70));
        Assert.True(cache.TryGet<int>("sliding", out var second));
        Assert.Equal(2, second);
        clock.Advance(TimeSpan.FromMilliseconds(120));
        Assert.False(cache.TryGet<int>("sliding", out _));
    }

    [Fact]
    public void InMemoryCacheDoesNotRetainMoreEntriesThanItsConfiguredSizeLimit()
    {
        using var provider = CreateProvider(sizeLimit: 1);
        var cache = provider.GetRequiredService<IInMemoryCacheManager>();

        cache.SetEntry("first", 1, TimeSpan.FromMinutes(1));
        cache.SetEntry("second", 2, TimeSpan.FromMinutes(1));

        var retained = 0;
        retained += cache.TryGet<int>("first", out _) ? 1 : 0;
        retained += cache.TryGet<int>("second", out _) ? 1 : 0;
        Assert.InRange(retained, 0, 1);
    }

    [Fact]
    public void InMemoryCacheReservesTheConfiguredMaximumWeightForEveryEntry()
    {
        using var cache = new InMemoryCacheManager(
            Options.Create(new CaeriusMemoryCacheOptions { SizeLimit = 3, MaxEntryWeight = 2 }),
            null);

        cache.SetEntry("first", 1, TimeSpan.FromMinutes(1));
        cache.SetEntry("second", 2, TimeSpan.FromMinutes(1));

        var retained = 0;
        retained += cache.TryGet<int>("first", out _) ? 1 : 0;
        retained += cache.TryGet<int>("second", out _) ? 1 : 0;
        Assert.InRange(retained, 0, 1);
    }

    [Fact]
    public async Task InMemoryCacheSingleFlightSurvivesACancelledFirstWaiter()
    {
        using var provider = CreateProvider();
        var cache = provider.GetRequiredService<IInMemoryCacheManager>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCalls = 0;

        async ValueTask<int> Factory(CancellationToken token)
        {
            Assert.True(token.CanBeCanceled);
            Interlocked.Increment(ref factoryCalls);
            started.TrySetResult();
            await release.Task.ConfigureAwait(false);
            return 73;
        }

        using var cancelledWaiter = new CancellationTokenSource();
        var first = cache.GetOrCreateAsync(
            "single-flight", TimeSpan.FromMinutes(1), false, Factory, cancelledWaiter.Token).AsTask();
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancelledWaiter.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        var others = Enumerable.Range(0, 24)
            .Select(_ => cache.GetOrCreateAsync(
                    "single-flight", TimeSpan.FromMinutes(1), false, Factory, TestContext.Current.CancellationToken)
                .AsTask())
            .ToArray();
        release.TrySetResult();

        Assert.All(await Task.WhenAll(others), static value => Assert.Equal(73, value));
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task InMemoryCacheCancelsTheSharedFactoryWhenTheLastWaiterLeaves()
    {
        using var provider = CreateProvider();
        var cache = provider.GetRequiredService<IInMemoryCacheManager>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async ValueTask<int> Factory(CancellationToken token)
        {
            started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                return 1;
            }
            catch (OperationCanceledException)
            {
                factoryCancelled.TrySetResult();
                throw;
            }
        }

        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        var first = cache.GetOrCreateAsync(
            "all-waiters-left", TimeSpan.FromMinutes(1), false, Factory, firstCancellation.Token).AsTask();
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        var second = cache.GetOrCreateAsync(
            "all-waiters-left", TimeSpan.FromMinutes(1), false, Factory, secondCancellation.Token).AsTask();

        firstCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(factoryCancelled.Task.IsCompleted);
        secondCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        await factoryCancelled.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InMemoryCacheKeepsTheSharedFactoryRunningWhileAnotherWaiterRemains()
    {
        using var provider = CreateProvider();
        var cache = provider.GetRequiredService<IInMemoryCacheManager>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async ValueTask<int> Factory(CancellationToken token)
        {
            started.TrySetResult();
            using var registration = token.Register(static state =>
                ((TaskCompletionSource)state!).TrySetResult(), factoryCancelled);
            await release.Task.WaitAsync(token).ConfigureAwait(false);
            return 73;
        }

        using var firstCancellation = new CancellationTokenSource();
        var first = cache.GetOrCreateAsync(
            "one-waiter-remains", TimeSpan.FromMinutes(1), false, Factory, firstCancellation.Token).AsTask();
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        var second = cache.GetOrCreateAsync(
                "one-waiter-remains", TimeSpan.FromMinutes(1), false, Factory, TestContext.Current.CancellationToken)
            .AsTask();

        firstCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(factoryCancelled.Task.IsCompleted);

        release.TrySetResult();
        Assert.Equal(73, await second);
        Assert.False(factoryCancelled.Task.IsCompleted);
    }

    [Fact]
    public async Task InMemoryCachePropagatesAFactoryFailureToAllWaitersAndAllowsRetry()
    {
        using var provider = CreateProvider();
        var cache = provider.GetRequiredService<IInMemoryCacheManager>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCalls = 0;

        async ValueTask<int> FailingFactory(CancellationToken _)
        {
            Interlocked.Increment(ref factoryCalls);
            started.TrySetResult();
            await release.Task.ConfigureAwait(false);
            throw new InvalidOperationException("Factory failure.");
        }

        var first = cache.GetOrCreateAsync(
            "factory-failure", TimeSpan.FromMinutes(1), false, FailingFactory, TestContext.Current.CancellationToken).AsTask();
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        var second = cache.GetOrCreateAsync(
            "factory-failure", TimeSpan.FromMinutes(1), false, FailingFactory, TestContext.Current.CancellationToken).AsTask();

        release.TrySetResult();

        Assert.Equal("Factory failure.", (await Assert.ThrowsAsync<InvalidOperationException>(() => first)).Message);
        Assert.Equal("Factory failure.", (await Assert.ThrowsAsync<InvalidOperationException>(() => second)).Message);
        Assert.Equal(1, factoryCalls);

        var retried = await cache.GetOrCreateAsync(
            "factory-failure",
            TimeSpan.FromMinutes(1),
            false,
            _ => ValueTask.FromResult(73),
            TestContext.Current.CancellationToken);
        Assert.Equal(73, retried);
    }

    [Fact]
    public async Task InMemoryCacheRemoveCannotBeUndoneByAnOlderInFlightPopulation()
    {
        using var provider = CreateProvider();
        var cache = provider.GetRequiredService<IInMemoryCacheManager>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCalls = 0;

        async ValueTask<int> StaleFactory(CancellationToken _)
        {
            Interlocked.Increment(ref factoryCalls);
            started.TrySetResult();
            await release.Task.ConfigureAwait(false);
            return 41;
        }

        var first = cache.GetOrCreateAsync(
            "invalidate-during-populate",
            TimeSpan.FromMinutes(1),
            false,
            StaleFactory,
            TestContext.Current.CancellationToken).AsTask();
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        cache.Remove("invalidate-during-populate");
        release.TrySetResult();

        Assert.Equal(41, await first);
        Assert.False(cache.TryGet<int>("invalidate-during-populate", out _));

        var refreshed = await cache.GetOrCreateAsync(
            "invalidate-during-populate",
            TimeSpan.FromMinutes(1),
            false,
            _ => ValueTask.FromResult(42),
            TestContext.Current.CancellationToken);
        Assert.Equal(42, refreshed);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task InMemoryCacheClearCannotBeUndoneByAnOlderInFlightPopulation()
    {
        using var provider = CreateProvider();
        var cache = provider.GetRequiredService<IInMemoryCacheManager>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async ValueTask<int> StaleFactory(CancellationToken _)
        {
            started.TrySetResult();
            await release.Task.ConfigureAwait(false);
            return 41;
        }

        var first = cache.GetOrCreateAsync(
            "clear-during-populate",
            TimeSpan.FromMinutes(1),
            false,
            StaleFactory,
            TestContext.Current.CancellationToken).AsTask();
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        cache.Clear();
        release.TrySetResult();

        Assert.Equal(41, await first);
        Assert.False(cache.TryGet<int>("clear-during-populate", out _));
        Assert.Equal(42, await cache.GetOrCreateAsync(
            "clear-during-populate",
            TimeSpan.FromMinutes(1),
            false,
            _ => ValueTask.FromResult(42),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FrozenCachePublishesOnlyCompleteSnapshotsAndNeverPopulatesOnMiss()
    {
        using var provider = CreateProvider();
        var cache = provider.GetRequiredService<IFrozenCacheManager>();
        cache.ReplaceSnapshot(new[]
        {
            new KeyValuePair<string, int>("north", 1),
            new KeyValuePair<string, int>("south", 2)
        });

        Assert.False(cache.TryGet<int>("not-in-snapshot", out _));
        Assert.False(cache.TryGet<int>("not-in-snapshot", out _));

        var errors = new ConcurrentQueue<string>();
        using var stopped = new CancellationTokenSource();
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stopped.IsCancellationRequested)
            {
                if (!cache.TryGet<int>("north", out var north) || north is not 1 and not 3)
                    errors.Enqueue("north was absent or only partially published");
                if (!cache.TryGet<int>("south", out var south) || south is not 2 and not 4)
                    errors.Enqueue("south was absent or only partially published");
            }
        }, TestContext.Current.CancellationToken)).ToArray();

        for (var index = 0; index < 500; index++)
        {
            cache.ReplaceSnapshot(new[]
            {
                new KeyValuePair<string, int>("north", 3),
                new KeyValuePair<string, int>("south", 4)
            });
            cache.ReplaceSnapshot(new[]
            {
                new KeyValuePair<string, int>("north", 1),
                new KeyValuePair<string, int>("south", 2)
            });
        }

        stopped.Cancel();
        await Task.WhenAll(readers).WaitAsync(TestContext.Current.CancellationToken);
        Assert.Empty(errors);
        Assert.False(cache.TryGet<int>("not-in-snapshot", out _));
    }

    [Fact]
    public async Task FrozenCacheDiscardsASnapshotThatBecameStaleDuringConstruction()
    {
        using var provider = CreateProvider();
        var cache = provider.GetRequiredService<IFrozenCacheManager>();
        cache.ReplaceSnapshot(new[] { new KeyValuePair<string, int>("current", 1) });

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        IEnumerable<KeyValuePair<string, int>> SlowSnapshot()
        {
            started.TrySetResult();
            yield return new KeyValuePair<string, int>("stale", 2);
            release.Task.GetAwaiter().GetResult();
            yield return new KeyValuePair<string, int>("also-stale", 3);
        }

        var replacement = Task.Run(() => cache.ReplaceSnapshot(SlowSnapshot()), TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        cache.Remove("stale");
        release.TrySetResult();
        await replacement.WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(cache.TryGet<int>("current", out var current));
        Assert.Equal(1, current);
        Assert.False(cache.TryGet<int>("stale", out _));
        Assert.False(cache.TryGet<int>("also-stale", out _));
    }

    [Fact]
    public async Task RedisLookupNonFatalFailuresFailOpenToTheSqlFactory()
    {
        var failures = new Exception[]
        {
            new TimeoutException("lookup timed out"),
            new System.IO.IOException("lookup transport failed"),
            new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionReset),
            new ObjectDisposedException("redis-multiplexer"),
            new CaeriusCacheException("get", new TimeoutException("wrapped lookup timed out")),
            new InvalidOperationException("third-party cache failure")
        };

        foreach (var failure in failures)
        {
            var remote = new DeterministicRemoteCache { LookupFailure = failure };
            using var provider = CreateProvider(remote);
            using var scope = provider.CreateScope();
            var context = (ICaeriusExecutionContext)scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
            var calls = 0;

            var result = await CaeriusExecutor.CachedAsync(
                context,
                RedisCommand($"Lookup{failure.GetType().Name}"),
                "integer",
                _ => ValueTask.FromResult(Interlocked.Increment(ref calls)),
                TestContext.Current.CancellationToken,
                new Int32Codec());

            Assert.Equal(1, result);
            Assert.Equal(1, calls);
            Assert.Equal(1, remote.LookupCalls);
            Assert.Equal(1, remote.SetCalls);
        }
    }

    [Fact]
    public async Task RedisPopulationNonFatalFailureReturnsTheSqlFactoryResult()
    {
        var remote = new DeterministicRemoteCache
        {
            SetFailure = new InvalidOperationException("third-party cache failure")
        };
        using var provider = CreateProvider(remote);
        using var scope = provider.CreateScope();
        var context = (ICaeriusExecutionContext)scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var calls = 0;

        var result = await CaeriusExecutor.CachedAsync(
            context,
            RedisCommand("PopulationFailOpen"),
            "integer",
            _ => ValueTask.FromResult(Interlocked.Increment(ref calls)),
            TestContext.Current.CancellationToken,
            new Int32Codec());

        Assert.Equal(1, result);
        Assert.Equal(1, calls);
        Assert.Equal(1, remote.LookupCalls);
        Assert.Equal(1, remote.SetCalls);
    }

    [Fact]
    public async Task RedisLookupCancellationIsNotTurnedIntoAFailOpenMiss()
    {
        var remote = new DeterministicRemoteCache { LookupCancellation = true };
        using var provider = CreateProvider(remote);
        using var scope = provider.CreateScope();
        var context = (ICaeriusExecutionContext)scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var calls = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CaeriusExecutor.CachedAsync(
            context,
            RedisCommand("LookupCancellation"),
            "integer",
            _ => ValueTask.FromResult(Interlocked.Increment(ref calls)),
            TestContext.Current.CancellationToken,
            new Int32Codec()).AsTask());

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task RedisReadThroughPopulationOwnsANewDependencyInjectionScope()
    {
        var remote = new DeterministicRemoteCache();
        using var provider = CreateProvider(remote);
        using var requestScope = provider.CreateScope();
        var requestContext =
            (ICaeriusExecutionContext)requestScope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var state = new ScopeCaptureState(requestContext);

        var value = await CaeriusExecutor.CachedAsync(
            requestContext,
            RedisCommand("OwnedPopulationScope"),
            "integer",
            state,
            static (populationContext, factoryState, _) =>
            {
                factoryState.PopulationContext = populationContext;
                return ValueTask.FromResult(17);
            },
            TestContext.Current.CancellationToken,
            new Int32Codec());

        Assert.Equal(17, value);
        Assert.NotNull(state.PopulationContext);
        Assert.NotSame(state.RequestContext, state.PopulationContext);
        Assert.Equal(state.RequestContext.Name, state.PopulationContext!.Name);
    }

    [Fact]
    public async Task RedisReadThroughPopulationResolvesTheSameKeyedContextInItsOwnedScope()
    {
        var remote = new DeterministicRemoteCache();
        var services = new ServiceCollection();
        services.AddCaeriusNet("reporting", options => options.UseSqlServer(
            "Server=.;Database=cache_reporting;Integrated Security=true;Encrypt=Strict;TrustServerCertificate=False"));
        services.AddSingleton<ICaeriusRemoteCacheManager>(remote);
        using var provider = services.BuildServiceProvider();
        using var requestScope = provider.CreateScope();
        var requestContext = (ICaeriusExecutionContext)requestScope.ServiceProvider
            .GetRequiredKeyedService<ICaeriusNetDbContext>("reporting");
        var state = new ScopeCaptureState(requestContext);

        var value = await CaeriusExecutor.CachedAsync(
            requestContext,
            RedisCommand("OwnedKeyedPopulationScope"),
            "integer",
            state,
            static (populationContext, factoryState, _) =>
            {
                factoryState.PopulationContext = populationContext;
                return ValueTask.FromResult(19);
            },
            TestContext.Current.CancellationToken,
            new Int32Codec());

        Assert.Equal(19, value);
        Assert.NotNull(state.PopulationContext);
        Assert.NotSame(state.RequestContext, state.PopulationContext);
        Assert.Equal("reporting", state.PopulationContext!.Name);
    }

    [Fact]
    public async Task RemoteInvalidationIsStrictAndWrapsTransportFailures()
    {
        var remote = new DeterministicRemoteCache { RemoveFailure = new InvalidOperationException("remove down") };
        using var provider = CreateProvider(remote);
        using var scope = provider.CreateScope();
        var context = (ICaeriusExecutionContext)scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var exception = await Assert.ThrowsAsync<CaeriusCacheException>(() =>
            CaeriusExecutor.InvalidateStrictAsync(context, ["one"], TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("invalidate-tags", exception.Operation);
        Assert.Equal(1, remote.RemoveCalls);
    }

    [Fact]
    public async Task RemoteInvalidationDoesNotWrapFatalFailures()
    {
        var failure = CreateSimulatedFatalFailure();
        var remote = new DeterministicRemoteCache { RemoveFailure = failure };
        using var provider = CreateProvider(remote);
        using var scope = provider.CreateScope();
        var context = (ICaeriusExecutionContext)scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var exception = await Assert.ThrowsAsync<OutOfMemoryException>(() =>
            CaeriusExecutor.InvalidateStrictAsync(context, ["one"], TestContext.Current.CancellationToken).AsTask());

        Assert.Same(failure, exception);
        Assert.Equal(1, remote.RemoveCalls);
    }

    [Fact]
    public async Task DurableSqlInvalidationFailureHasANonRetryableStateException()
    {
        var remote = new DeterministicRemoteCache { RemoveFailure = new InvalidOperationException("remove down") };
        using var provider = CreateProvider(remote);
        using var scope = provider.CreateScope();
        var context = (ICaeriusExecutionContext)scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var exception = await Assert.ThrowsAsync<CaeriusCacheInvalidationException>(() =>
            CaeriusExecutor.InvalidateAfterDurableSqlAsync(context, ["orders"]).AsTask());

        Assert.True(exception.SqlWasCommitted);
        Assert.IsType<CaeriusCacheException>(exception.InnerException);
        Assert.Contains("Do not blindly retry", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DurableSqlInvalidationDoesNotWrapFatalFailures()
    {
        var failure = CreateSimulatedFatalFailure();
        var remote = new DeterministicRemoteCache { RemoveFailure = failure };
        using var provider = CreateProvider(remote);
        using var scope = provider.CreateScope();
        var context = (ICaeriusExecutionContext)scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var exception = await Assert.ThrowsAsync<OutOfMemoryException>(() =>
            CaeriusExecutor.InvalidateAfterDurableSqlAsync(context, ["orders"]).AsTask());

        Assert.Same(failure, exception);
        Assert.Equal(1, remote.RemoveCalls);
    }

    [Fact]
    public async Task MultiResultAggregateUsesOneRemoteValueAndRoundtripsEverySetTogether()
    {
        var remote = new DeterministicRemoteCache();
        using var provider = CreateProvider(remote);
        using var scope = provider.CreateScope();
        var context = (ICaeriusExecutionContext)scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var aggregateCodec = new CaeriusAggregateCacheCodec([new Int32Codec(), new StringCodec()]);
        var payload = new object[] { 17, "all-or-nothing" };
        var encoded = new ArrayBufferWriter<byte>();
        aggregateCodec.Encode(encoded, payload);
        Assert.Equal(payload, aggregateCodec.Decode(encoded.WrittenSpan));

        var factoryCalls = 0;
        var command = RedisCommand("Aggregate");
        var first = await CaeriusExecutor.CachedAsync(
            context,
            command,
            "multi:2:array",
            _ => ValueTask.FromResult<object[]>([Interlocked.Increment(ref factoryCalls), "all-or-nothing"]),
            TestContext.Current.CancellationToken,
            aggregateCodec);
        var second = await CaeriusExecutor.CachedAsync(
            context,
            command,
            "multi:2:array",
            _ => ValueTask.FromResult<object[]>([999, "must-not-run"]),
            TestContext.Current.CancellationToken,
            aggregateCodec);

        Assert.Equal([1, "all-or-nothing"], first);
        Assert.Equal(first, second);
        Assert.Equal(1, factoryCalls);
        Assert.Equal(2, remote.LookupCalls);
        Assert.Equal(1, remote.SetCalls);
        Assert.Single(remote.StoredValues);
    }

    [Fact]
    public void MultiResultAggregateRejectsCorruptedOrTrailingPayloads()
    {
        var aggregateCodec = new CaeriusAggregateCacheCodec([new Int32Codec(), new StringCodec()]);
        var payload = new ArrayBufferWriter<byte>();
        aggregateCodec.Encode(payload, [17, "all-or-nothing"]);

        var trailing = new byte[payload.WrittenCount + 1];
        payload.WrittenSpan.CopyTo(trailing);
        trailing[^1] = 0xFF;

        Assert.Throws<CaeriusContractException>(() => aggregateCodec.Decode(trailing));
        Assert.Throws<CaeriusContractException>(() => aggregateCodec.Decode(payload.WrittenSpan[..4]));
    }

    private static ServiceProvider CreateProvider(DeterministicRemoteCache? remote = null, long sizeLimit = 32)
    {
        var services = new ServiceCollection();
        services.Configure<CaeriusMemoryCacheOptions>(options => options.SizeLimit = sizeLimit);
        services.AddCaeriusNet(options => options.UseSqlServer(
            "Server=.;Database=cache_acceptance;Integrated Security=true;Encrypt=Strict;TrustServerCertificate=False"));
        if (remote is not null)
            services.AddSingleton<ICaeriusRemoteCacheManager>(remote);
        return services.BuildServiceProvider();
    }

    private static StoredProcedureCommand RedisCommand(string procedureName)
    {
        return new StoredProcedureCommandBuilder("dbo", procedureName)
            .UseRedisCache(TimeSpan.FromMinutes(1))
            .Build();
    }

    private static OutOfMemoryException CreateSimulatedFatalFailure()
    {
#pragma warning disable CA2201 // The tests need a representative fatal runtime exception to verify it is never wrapped.
        return new OutOfMemoryException("simulated fatal cache failure");
#pragma warning restore CA2201
    }

    private sealed class DeterministicRemoteCache : ICaeriusRemoteCacheManager
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

        public Exception? LookupFailure { get; init; }
        public Exception? SetFailure { get; init; }
        public Exception? RemoveFailure { get; init; }
        public bool LookupCancellation { get; init; }
        public int LookupCalls { get; private set; }
        public int SetCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public IReadOnlyDictionary<string, object> StoredValues => _values;

        public ValueTask<CaeriusCacheLookup<T>> TryGetAsync<T>(
            string key,
            ICaeriusCacheCodec<T> codec,
            CancellationToken cancellationToken = default)
        {
            LookupCalls++;
            if (LookupCancellation)
                throw new OperationCanceledException(
                    "The deterministic lookup was cancelled.",
                    cancellationToken.CanBeCanceled ? cancellationToken : new CancellationToken(true));
            if (LookupFailure is not null)
                return ValueTask.FromException<CaeriusCacheLookup<T>>(LookupFailure);
            return ValueTask.FromResult(_values.TryGetValue(key, out var value) && value is T typed
                ? CaeriusCacheLookup<T>.Hit(typed)
                : CaeriusCacheLookup<T>.Miss);
        }

        public ValueTask SetAsync<T>(
            string key,
            T value,
            ICaeriusCacheCodec<T> codec,
            TimeSpan? expiration,
            CancellationToken cancellationToken = default) where T : notnull
        {
            SetCalls++;
            if (SetFailure is not null)
                return ValueTask.FromException(SetFailure);
            _values[key] = value;
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            RemoveCalls++;
            if (RemoveFailure is not null)
                return ValueTask.FromException(RemoveFailure);
            _values.Remove(key);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ScopeCaptureState(ICaeriusExecutionContext requestContext)
    {
        internal ICaeriusExecutionContext RequestContext { get; } = requestContext;
        internal ICaeriusExecutionContext? PopulationContext { get; set; }
    }

    private sealed class Int32Codec : ICaeriusCacheCodec<int>
    {
        public string CodecId => "tests/int32/v1";
        public string ContractHash => "82d31d6a";

        public void Encode(IBufferWriter<byte> writer, int value)
        {
            new CaeriusBinaryWriter(writer).WriteInt32(value);
        }

        public int Decode(ReadOnlySpan<byte> payload)
        {
            var reader = new CaeriusBinaryReader(payload);
            var value = reader.ReadInt32();
            if (reader.Remaining != 0)
                throw new CaeriusContractException("The integer cache payload contains trailing data.");
            return value;
        }
    }

    private sealed class StringCodec : ICaeriusCacheCodec<string>
    {
        public string CodecId => "tests/string/v1";
        public string ContractHash => "b1842caa";

        public void Encode(IBufferWriter<byte> writer, string value)
        {
            new CaeriusBinaryWriter(writer).WriteString(value);
        }

        public string Decode(ReadOnlySpan<byte> payload)
        {
            var reader = new CaeriusBinaryReader(payload);
            var value = reader.ReadString();
            if (reader.Remaining != 0)
                throw new CaeriusContractException("The string cache payload contains trailing data.");
            return value;
        }
    }

    private sealed class ManualSystemClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public void Advance(TimeSpan duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
            UtcNow += duration;
        }
    }
}
