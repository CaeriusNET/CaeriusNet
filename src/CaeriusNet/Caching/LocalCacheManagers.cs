using Microsoft.Extensions.Internal;

namespace CaeriusNet.Caching;

/// <summary>
///     Bounded dynamic cache with per-key single-flight population.
/// </summary>
/// <remarks>
///     Mutations and population publication share <see cref="_mutationLock" />.  This is intentionally not on the
///     read-hit path: a write invalidation that completes after a factory began can never be followed by that
///     factory repopulating the removed value.
/// </remarks>
internal sealed class InMemoryCacheManager : IInMemoryCacheManager, IDisposable
{
    private readonly MemoryCache _cache;
    private readonly Dictionary<string, InFlight> _flights = new(StringComparer.Ordinal);
    private readonly int _maxConcurrentPopulations;
    private readonly long _maxEntryWeight;
    private readonly object _mutationLock = new();
    private long _clearGeneration;

    public InMemoryCacheManager(IOptions<CaeriusMemoryCacheOptions> options)
        : this(options, null)
    {
    }

    // The clock is deliberately internal-construction-only. Production relies on MemoryCache's system clock;
    // tests can supply a deterministic clock without exposing another runtime option or sleeping.
    internal InMemoryCacheManager(
        IOptions<CaeriusMemoryCacheOptions> options,
        ISystemClock? clock)
    {
        var sizeLimit = options.Value.SizeLimit;
        if (sizeLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Memory cache size limit must be positive.");
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimit, Clock = clock });
        if (options.Value.MaxEntryWeight <= 0 || options.Value.MaxEntryWeight > sizeLimit)
            throw new ArgumentOutOfRangeException(nameof(options),
                "Maximum entry weight must be positive and not exceed the size limit.");
        // The generic cache API deliberately has no object-size estimator. Reserve the configured
        // worst-case weight for every value so SizeLimit remains a real bound even for arbitrary DTOs.
        _maxEntryWeight = options.Value.MaxEntryWeight;
        _maxConcurrentPopulations = options.Value.MaxConcurrentPopulations;
        if (_maxConcurrentPopulations <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum concurrent populations must be positive.");
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    public bool TryGet<T>(string key, out T? value)
    {
        ValidateKey(key);
        if (_cache.TryGetValue(key, out var cached) && cached is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public void SetEntry<T>(string key, T value, TimeSpan expiration, bool slidingExpiration = false) where T : notnull
    {
        ValidateKey(key);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiration, TimeSpan.Zero);
        lock (_mutationLock)
        {
            // An explicit write is newer than an existing read-through population for the same key.
            InvalidateFlightUnderLock(key);
            SetEntryUnderLock(key, value, expiration, slidingExpiration);
        }
    }

    public ValueTask<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan expiration,
        bool slidingExpiration,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken = default) where T : notnull
    {
        return GetOrCreateAsync<T, Func<CancellationToken, ValueTask<T>>>(
            key,
            expiration,
            slidingExpiration,
            factory,
            static (factoryState, token) => factoryState(token),
            cancellationToken);
    }

    public async ValueTask<T> GetOrCreateAsync<T, TState>(
        string key,
        TimeSpan expiration,
        bool slidingExpiration,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken = default) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (TryGet<T>(key, out var cached))
            return cached!;

        InFlight flight;
        var ownsFlight = false;
        lock (_mutationLock)
        {
            // A writer may have populated the cache between the optimistic read and this lock.
            if (_cache.TryGetValue(key, out var synchronizedCached) && synchronizedCached is T typed)
                return typed;

            if (!_flights.TryGetValue(key, out flight!))
            {
                if (_flights.Count >= _maxConcurrentPopulations)
                    throw new CaeriusCacheException(
                        "populate-limit",
                        new InvalidOperationException("The in-memory cache population limit was reached."));
                flight = new InFlight(_clearGeneration);
                _flights.Add(key, flight);
                ownsFlight = true;
            }

            checked
            {
                flight.Waiters++;
            }
        }

        if (ownsFlight)
            _ = PopulateAsync(key, flight, expiration, slidingExpiration, state, factory);

        return await WaitForFlightAsync<T>(flight, cancellationToken).ConfigureAwait(false);
    }

    public void Remove(string key)
    {
        ValidateKey(key);
        lock (_mutationLock)
        {
            InvalidateFlightUnderLock(key);
            _cache.Remove(key);
        }
    }

    public void Clear()
    {
        lock (_mutationLock)
        {
            checked
            {
                _clearGeneration++;
            }

            foreach (var flight in _flights.Values)
            {
                flight.Invalidated = true;
                flight.PopulationCancellation.Cancel();
            }

            _flights.Clear();
            _cache.Clear();
        }
    }

    private async Task PopulateAsync<T, TState>(
        string key,
        InFlight flight,
        TimeSpan expiration,
        bool slidingExpiration,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory) where T : notnull
    {
        try
        {
            // The population belongs to the cache key rather than its first waiter. A cancelled caller therefore
            // only stops waiting; it must not cancel the shared work and make every other waiter fail.
            var value = await factory(state, flight.PopulationCancellation.Token).ConfigureAwait(false);
            lock (_mutationLock)
            {
                if (!flight.Invalidated && flight.ClearGeneration == _clearGeneration)
                    SetEntryUnderLock(key, value, expiration, slidingExpiration);
                RemoveFlightIfCurrentUnderLock(key, flight);
            }

            flight.Completion.TrySetResult(value);
        }
        catch (Exception exception)
        {
            lock (_mutationLock)
            {
                RemoveFlightIfCurrentUnderLock(key, flight);
            }

            flight.Completion.TrySetException(exception);
            // A shared work item can fault after every waiter cancelled. Explicitly observe that exception.
            _ = flight.Completion.Task.Exception;
        }
        finally
        {
            flight.PopulationCancellation.Dispose();
        }
    }

    private void InvalidateFlightUnderLock(string key)
    {
        if (_flights.Remove(key, out var flight))
        {
            flight.Invalidated = true;
            flight.PopulationCancellation.Cancel();
        }
    }

    private void RemoveFlightIfCurrentUnderLock(string key, InFlight flight)
    {
        if (_flights.TryGetValue(key, out var current) && ReferenceEquals(current, flight))
            _flights.Remove(key);
    }

    private void SetEntryUnderLock<T>(string key, T value, TimeSpan expiration, bool slidingExpiration)
        where T : notnull
    {
        var options = new MemoryCacheEntryOptions().SetSize(_maxEntryWeight);
        if (slidingExpiration)
            options.SetSlidingExpiration(expiration);
        else
            options.SetAbsoluteExpiration(expiration);
        _cache.Set(key, value, options);
    }

    private static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
    }

    private async ValueTask<T> WaitForFlightAsync<T>(InFlight flight, CancellationToken cancellationToken)
    {
        try
        {
            return (T)await flight.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            var cancel = false;
            lock (_mutationLock)
            {
                flight.Waiters--;
                cancel = flight.Waiters == 0 && !flight.Completion.Task.IsCompleted;
            }

            if (cancel)
                flight.PopulationCancellation.Cancel();
        }
    }

    private sealed class InFlight(long clearGeneration)
    {
        internal TaskCompletionSource<object> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal CancellationTokenSource PopulationCancellation { get; } = new();

        internal long ClearGeneration { get; } = clearGeneration;
        internal bool Invalidated { get; set; }
        internal int Waiters { get; set; }
    }
}

/// <summary>
///     Read-optimized snapshot cache. Reads never take a lock or cause a rebuild; writers publish an entire frozen
///     dictionary atomically.
/// </summary>
internal sealed class FrozenCacheManager : IFrozenCacheManager
{
    private readonly object _writeLock = new();
    private long _generation;
    private FrozenDictionary<string, object> _snapshot = FrozenDictionary<string, object>.Empty;

    public bool TryGet<T>(string key, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot.TryGetValue(key, out var cached) && cached is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public void ReplaceSnapshot<T>(IEnumerable<KeyValuePair<string, T>> entries) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(entries);
        var observedGeneration = Volatile.Read(ref _generation);
        var capacity = entries.TryGetNonEnumeratedCount(out var count) ? count : 0;
        var builder = new Dictionary<string, object>(capacity, StringComparer.Ordinal);
        foreach (var pair in entries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            if (!builder.TryAdd(pair.Key, pair.Value))
                throw new ArgumentException($"Duplicate frozen-cache key '{pair.Key}'.", nameof(entries));
        }

        var next = builder.ToFrozenDictionary(StringComparer.Ordinal);
        lock (_writeLock)
        {
            // A snapshot that started before an invalidation or another publication is stale. Discard it instead
            // of letting an old refresh undo a successful Remove/Clear. The caller may schedule a new refresh.
            if (observedGeneration != _generation)
                return;

            Volatile.Write(ref _snapshot, next);
            checked
            {
                _generation++;
            }
        }
    }

    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_writeLock)
        {
            var current = Volatile.Read(ref _snapshot);
            if (!current.ContainsKey(key))
            {
                // A missing entry can still be present in a snapshot currently being built. Advancing the
                // generation prevents that older publication from resurrecting it after this invalidation.
                checked
                {
                    _generation++;
                }

                return;
            }

            var builder = new Dictionary<string, object>(current.Count - 1, StringComparer.Ordinal);
            foreach (var pair in current)
                if (!string.Equals(pair.Key, key, StringComparison.Ordinal))
                    builder.Add(pair.Key, pair.Value);

            Volatile.Write(ref _snapshot, builder.ToFrozenDictionary(StringComparer.Ordinal));
            checked
            {
                _generation++;
            }
        }
    }

    public void Clear()
    {
        lock (_writeLock)
        {
            Volatile.Write(ref _snapshot, FrozenDictionary<string, object>.Empty);
            checked
            {
                _generation++;
            }
        }
    }
}
