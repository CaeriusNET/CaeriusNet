namespace CaeriusNet.Caching;

/// <summary>
///     Coordinates one in-process Redis read-through operation for each canonical cache key.
/// </summary>
/// <remarks>
///     Redis is deliberately not used as a distributed lock.  This coordinator only collapses a burst from scopes
///     in the current host; the remote lookup, SQL fallback and Redis population form one shared operation.  A
///     cancelled caller stops waiting without cancelling that operation, so another caller can still receive the
///     populated value.  The typed flight avoids boxing a struct state or cache value.
/// </remarks>
internal sealed class RedisReadThroughCoordinator
{
    private readonly Dictionary<string, IFlight> _flights = new(StringComparer.Ordinal);
    private readonly object _gate = new();

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
                flight = new InFlight<TValue>();
                _flights.Add(key, flight);
                ownsFlight = true;
            }
        }

        if (ownsFlight)
            _ = PopulateAsync(key, flight, state, factory);

        return WaitAsync(flight.Completion.Task, cancellationToken);
    }

    private static ValueTask<TValue> WaitAsync<TValue>(Task<TValue> task, CancellationToken cancellationToken)
    {
        return cancellationToken.CanBeCanceled
            ? new ValueTask<TValue>(task.WaitAsync(cancellationToken))
            : new ValueTask<TValue>(task);
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
            flight.Completion.TrySetResult(await factory(state, CancellationToken.None).ConfigureAwait(false));
        }
        catch (Exception exception)
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
        }
    }

    private interface IFlight;

    private sealed class InFlight<TValue> : IFlight
    {
        internal TaskCompletionSource<TValue> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
