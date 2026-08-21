using CaeriusNet.Caching;
using CaeriusNet.Exceptions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CaeriusNet.Tests.Caching;

public sealed class RedisReadThroughCoordinatorTests
{
    [Fact]
    public async Task CancelsTheSharedPopulationWhenTheLastWaiterLeaves()
    {
        var coordinator = CreateCoordinator();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callerCancellation = new CancellationTokenSource();

        async ValueTask<int> Factory(int _, CancellationToken populationToken)
        {
            started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, populationToken).ConfigureAwait(false);
                return 1;
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }
        }

        var waiter = coordinator.GetOrCreateAsync(
            "cancel-last-waiter",
            0,
            Factory,
            callerCancellation.Token).AsTask();
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);

        callerCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        await cancelled.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnforcesTheConfiguredConcurrentPopulationLimit()
    {
        var coordinator = CreateCoordinator(1);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async ValueTask<int> FirstFactory(int state, CancellationToken populationToken)
        {
            _ = state;
            _ = populationToken;
            started.TrySetResult();
            await release.Task.ConfigureAwait(false);
            return 1;
        }

        var first = coordinator.GetOrCreateAsync(
            "first",
            0,
            FirstFactory,
            TestContext.Current.CancellationToken).AsTask();
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);

        var exception = Assert.Throws<CaeriusCacheException>(() =>
        {
            _ = coordinator.GetOrCreateAsync(
                "second",
                0,
                static (_, _) => ValueTask.FromResult(2),
                TestContext.Current.CancellationToken).AsTask();
        });

        Assert.Equal("populate-limit", exception.Operation);
        release.TrySetResult();
        Assert.Equal(1, await first);
    }

    [Fact]
    public async Task DoesNotAttachANewWaiterToAPopulationCancelledAfterTheLastWaiterLeft()
    {
        var coordinator = CreateCoordinator();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var firstCancellation = new CancellationTokenSource();

        async ValueTask<int> UncooperativeFirstFactory(int _, CancellationToken populationToken)
        {
            firstStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, populationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Simulate an I/O provider which only observes cancellation after it reaches its own boundary.
            }

            await allowFirstToFinish.Task.ConfigureAwait(false);
            firstFinished.TrySetResult();
            return 1;
        }

        var first = coordinator.GetOrCreateAsync(
            "rejoin-after-cancel",
            0,
            UncooperativeFirstFactory,
            firstCancellation.Token).AsTask();
        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        firstCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        var secondCalls = 0;
        var second = await coordinator.GetOrCreateAsync(
            "rejoin-after-cancel",
            0,
            (_, _) => ValueTask.FromResult(Interlocked.Increment(ref secondCalls) + 1),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, second);
        Assert.Equal(1, secondCalls);
        allowFirstToFinish.TrySetResult();
        await firstFinished.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PropagatesAFactoryFailureToAllWaitersAndAllowsRetry()
    {
        var coordinator = CreateCoordinator();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCalls = 0;

        async ValueTask<int> FailingFactory(int _, CancellationToken __)
        {
            Interlocked.Increment(ref factoryCalls);
            started.TrySetResult();
            await release.Task.ConfigureAwait(false);
            throw new InvalidOperationException("Factory failure.");
        }

        var first = coordinator.GetOrCreateAsync(
            "factory-failure", 0, FailingFactory, TestContext.Current.CancellationToken).AsTask();
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        var second = coordinator.GetOrCreateAsync(
            "factory-failure", 0, FailingFactory, TestContext.Current.CancellationToken).AsTask();

        release.TrySetResult();

        Assert.Equal("Factory failure.", (await Assert.ThrowsAsync<InvalidOperationException>(() => first)).Message);
        Assert.Equal("Factory failure.", (await Assert.ThrowsAsync<InvalidOperationException>(() => second)).Message);
        Assert.Equal(1, factoryCalls);

        var retried = await coordinator.GetOrCreateAsync(
            "factory-failure", 0, static (_, _) => ValueTask.FromResult(73), TestContext.Current.CancellationToken);
        Assert.Equal(73, retried);
    }

    private static RedisReadThroughCoordinator CreateCoordinator(int maxConcurrentPopulations = 256)
    {
        return new RedisReadThroughCoordinator(Options.Create(new CaeriusMemoryCacheOptions
        {
            MaxConcurrentPopulations = maxConcurrentPopulations
        }));
    }
}
