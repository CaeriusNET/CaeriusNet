namespace CaeriusNet.Redis.Abstractions;

/// <summary>
///     Provides access to a singleton <see cref="ConnectionMultiplexer" /> for a given Redis connection string
///     and exposes database retrieval in a strictly asynchronous, cancellation-aware manner.
/// </summary>
public interface IRedisConnectionProvider : IAsyncDisposable
{
    /// <summary>
    ///     Gets the configured default database index for this provider.
    /// </summary>
    int DefaultDatabase { get; }

    /// <summary>
    ///     Ensures that a shared <see cref="ConnectionMultiplexer" /> is established and ready.
    ///     The underlying connect continues in the background if the token is canceled.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting.</param>
    /// <returns>A <see cref="ValueTask" /> representing the asynchronous operation.</returns>
    ValueTask EnsureConnectedAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Returns whether the provider currently holds a connected and usable multiplexer instance.
    /// </summary>
    /// <param name="cancellationToken">A token to observe. If already canceled, the method returns <c>false</c>.</param>
    /// <returns>A <see cref="ValueTask{TResult}" /> yielding <c>true</c> if connected; otherwise, <c>false</c>.</returns>
    ValueTask<bool> IsConnectedAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Retrieves a Redis <see cref="IDatabase" /> instance for the specified database index,
    ///     defaulting to <see cref="DefaultDatabase" /> when <paramref name="db" /> is <c>null</c>.
    /// </summary>
    /// <param name="db">An optional database index; if <c>null</c>, the provider default is used.</param>
    /// <param name="cancellationToken">A token to observe while preparing access.</param>
    /// <returns>A <see cref="ValueTask{TResult}" /> that yields an <see cref="IDatabase" />.</returns>
    ValueTask<IDatabase> GetDatabaseAsync(int? db, CancellationToken cancellationToken);
}