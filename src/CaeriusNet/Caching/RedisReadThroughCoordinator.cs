namespace CaeriusNet.Caching;

/// <summary>
///     Coordinates one in-process Redis read-through operation for each canonical cache key.
/// </summary>
/// <remarks>
///     Redis is deliberately not used as a distributed lock. This coordinator only collapses a burst from scopes
///     in the current host; the remote lookup, SQL fallback and Redis population form one shared operation. A
///     cancelled caller stops waiting without cancelling work still needed by another caller. When the last waiter
///     leaves, its shared cancellation token is cancelled so no orphaned SQL/Redis population survives. The typed
///     flight avoids boxing a struct state or cache value.
/// </remarks>
internal sealed class RedisReadThroughCoordinator
{
    private readonly Dictionary<string, IFlight> _flights = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly int _maxConcurrentPopulations;

    public RedisReadThroughCoordinator(IOptions<CaeriusMemoryCacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _maxConcurrentPopulations = options.Value.MaxConcurrentPopulations;
        if (_maxConcurrentPopulations <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum concurrent populations must be positive.");
    }

    internal ValueTask<TValue> GetOrCreateAsync<TValue, TState>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<TValue>> factory,
        CancellationToken cancellationToken) where TValue : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled<TValue>(cancellationToken);

        InFlight<TValue> flight;
        var ownsFlight = false;
        lock (_gate)
        {
            if (_flights.TryGetValue(key, out var existing))
            {
                flight = existing as InFlight<TValue> ?? throw new CaeriusContractException(
                    "The same Redis cache key was requested with incompatible result types. Ensure result shapes and codecs are unique.");
            }
            else
            {
                if (_flights.Count >= _maxConcurrentPopulations)
                    throw new CaeriusCacheException(
                        "populate-limit",
                        new InvalidOperationException("The Redis read-through population limit was reached."));
                flight = new InFlight<TValue>();
                _flights.Add(key, flight);
                ownsFlight = true;
            }

            checked
            {
                flight.Waiters++;
            }
        }

        if (ownsFlight)
            _ = PopulateAsync(key, flight, state, factory);

        return WaitAsync(key, flight, cancellationToken);
    }

    private async ValueTask<TValue> WaitAsync<TValue>(
        string key,
        InFlight<TValue> flight,
        CancellationToken cancellationToken)
    {
        try
        {
            return cancellationToken.CanBeCanceled
                ? await flight.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false)
                : await flight.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            var cancel = false;
            lock (_gate)
            {
                flight.Waiters--;
                cancel = flight.Waiters == 0 && !flight.Completion.Task.IsCompleted;
                if (cancel && _flights.TryGetValue(key, out var current) && ReferenceEquals(current, flight))
                    _flights.Remove(key);
            }

            if (cancel)
                flight.PopulationCancellation.Cancel();
        }
    }

    private async Task PopulateAsync<TValue, TState>(
        string key,
        InFlight<TValue> flight,
        TState state,
        Func<TState, CancellationToken, ValueTask<TValue>> factory) where TValue : notnull
    {
        try
        {
            // The work is owned by the canonical key rather than the caller that won the race.
            flight.Completion.TrySetResult(
                await factory(state, flight.PopulationCancellation.Token).ConfigureAwait(false));
        }
        catch (Exception exception) when (IsNonFatal(exception))
        {
            flight.Completion.TrySetException(exception);
        }
        finally
        {
            lock (_gate)
            {
                if (_flights.TryGetValue(key, out var current) && ReferenceEquals(current, flight))
                    _flights.Remove(key);
            }

            flight.PopulationCancellation.Dispose();
        }
    }

    private static bool IsNonFatal(Exception exception)
    {
        return exception is not OutOfMemoryException
            and not StackOverflowException
            and not AccessViolationException
            and not AppDomainUnloadedException
            and not BadImageFormatException
            and not CannotUnloadAppDomainException
            and not System.Threading.ThreadAbortException;
    }

    private interface IFlight;

    private sealed class InFlight<TValue> : IFlight
    {
        internal TaskCompletionSource<TValue> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal CancellationTokenSource PopulationCancellation { get; } = new();
        internal int Waiters { get; set; }
    }
}
