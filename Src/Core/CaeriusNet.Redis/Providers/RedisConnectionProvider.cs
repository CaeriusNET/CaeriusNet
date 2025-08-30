using System.Diagnostics.CodeAnalysis;
using static System.Threading.Tasks.ValueTask;

namespace CaeriusNet.Redis.Providers;

/// <summary>
///     Default implementation of <see cref="IRedisConnectionProvider" /> maintaining a single long-lived
///     ConnectionMultiplexer per connection string. All APIs are async-only and cancellation aware.
/// </summary>
internal sealed class RedisConnectionProvider : IRedisConnectionProvider
{
    // Cached completed tasks to minimize allocations for frequently returned completed operations.
    private static readonly ValueTask<bool> SFalseValueTaskFalse;

    // Using a Lazy<Task<ConnectionMultiplexer>> ensures exactly-once async initialization without locks on the hot path.
    private readonly Lazy<Task<ConnectionMultiplexer>> _lazyMultiplexerTask;

    static RedisConnectionProvider()
    {
        SFalseValueTaskFalse = new ValueTask<bool>( /* False */);
    }

    public RedisConnectionProvider(string connectionString, int defaultDatabase)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Redis connection string must be a non-empty value.", nameof(connectionString));

        DefaultDatabase = defaultDatabase;

        // Configure resilient defaults while allowing server-side default DB when -1 is used.
        _lazyMultiplexerTask = new Lazy<Task<ConnectionMultiplexer>>(
            () => ConnectInternalAsync(connectionString, defaultDatabase),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public int DefaultDatabase { get; }

    /// <inheritdoc />
    public async ValueTask EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // If already created, just await it (cheap if already completed).
        var task = _lazyMultiplexerTask.Value;

        // Allow the caller to stop waiting; the connect continues in background.
        await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask<bool> IsConnectedAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return SFalseValueTaskFalse;

        if (!_lazyMultiplexerTask.IsValueCreated)
            return FromResult(false);

        var task = _lazyMultiplexerTask.Value;
        if (!task.IsCompletedSuccessfully)
            return FromResult(false);

        var mux = task.Result;
        var connected = mux.IsConnected && mux.GetStatus() is not null; // GetStatus() causes a lightweight probe.
        return FromResult(connected);
    }

    /// <inheritdoc />
    public async ValueTask<IDatabase> GetDatabaseAsync(int? db, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mux = await GetMultiplexerAsync(cancellationToken).ConfigureAwait(false);
        // IDatabase is a cheap proxy; no caching needed. Use configured default when db is null.
        return mux.GetDatabase(db ?? DefaultDatabase);
    }

    public async ValueTask DisposeAsync()
    {
        if (_lazyMultiplexerTask.IsValueCreated)
            try
            {
                var mux = await _lazyMultiplexerTask.Value.ConfigureAwait(false);
                await mux.CloseAsync().ConfigureAwait(false);
                await mux.DisposeAsync();
            }
            catch
            {
                // Swallow disposal-time exceptions to avoid throwing on container shutdown.
            }
    }

    private static async Task<ConnectionMultiplexer> ConnectInternalAsync(string connectionString, int defaultDb)
    {
        // Parse options to control resiliency and keep-alives.
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false; // allow reconnects
        options.KeepAlive = Math.Max(options.KeepAlive, 15); // seconds; ensure some periodic heartbeat
        if (defaultDb >= 0)
            options.DefaultDatabase = defaultDb;

        // ConnectAsync is already async; no sync-over-async anywhere.
        var mux = await ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(false);

        // Proactively register event handlers with minimal allocations to aid diagnostics without heavy logging.
        mux.ConnectionFailed += static (_, _) => { };
        mux.ConnectionRestored += static (_, _) => { };
        mux.ConfigurationChanged += static (_, _) => { };
        mux.ErrorMessage += static (_, _) => { };

        return mux;
    }

    [SuppressMessage("Usage", "CA1822:Mark members as static",
        Justification = "Non-static for clarity and future extension.")]
    private async ValueTask<ConnectionMultiplexer> GetMultiplexerAsync(CancellationToken cancellationToken)
    {
        var task = _lazyMultiplexerTask.Value;
        await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
    }
}