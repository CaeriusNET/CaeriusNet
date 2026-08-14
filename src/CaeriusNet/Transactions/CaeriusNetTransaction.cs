using CaeriusNet.Execution;

namespace CaeriusNet.Transactions;

internal sealed class CaeriusNetTransaction : ICaeriusNetTransaction
{
    private const int Active = 0;
    private const int Completing = 1;
    private const int Completed = 2;
    private const int Disposing = 3;
    private const int Disposed = 4;
    private readonly HashSet<string> _deferredInvalidations = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly ICaeriusSqlConnectionLease _lease;
    private int _commandInFlight;
    private int _poisoned;
    private int _state;

    private CaeriusNetTransaction(
        ICaeriusExecutionContext context,
        ICaeriusSqlConnectionLease lease,
        SqlTransaction transaction)
    {
        Context = context;
        _lease = lease;
        Transaction = transaction;
    }

    internal SqlConnection Connection => _lease.Connection;
    internal SqlTransaction Transaction { get; }
    internal ICaeriusExecutionContext Context { get; }

    public bool IsActive => Volatile.Read(ref _state) == Active && Volatile.Read(ref _poisoned) == 0;

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string[] invalidations;
        lock (_gate)
        {
            if (_state != Active)
                throw new InvalidOperationException("The transaction is no longer active.");
            if (Volatile.Read(ref _poisoned) != 0)
                throw new InvalidOperationException("A poisoned transaction cannot be committed; roll it back.");
            if (_commandInFlight != 0)
                throw new InvalidOperationException("A command is still running on this transaction.");

            // Reserve terminal ownership before releasing the lock so that a new command, rollback, or disposal
            // can never race the physical commit and close its connection.
            Volatile.Write(ref _state, Completing);
            invalidations = [.. _deferredInvalidations];
            _deferredInvalidations.Clear();
        }

        try
        {
            // A caller may cancel while the provider is writing COMMIT or waiting for its acknowledgement. At
            // that point the server may already have made the transaction durable, so surfacing cancellation
            // would make the outcome ambiguous and could skip the cache invalidations below. Cancellation is
            // deliberately observed only by the pre-check above; once terminal ownership is reserved, commit
            // and its strictly-correct cache follow-up run to completion without caller cancellation.
            await Transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (SqlException exception)
        {
            throw new CaeriusNetSqlException("<commit-transaction>", exception);
        }
        finally
        {
            try
            {
                await Transaction.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await _lease.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    Volatile.Write(ref _state, Completed);
                }
            }
        }

        // COMMIT succeeded. Do not let a token cancelled after the pre-check leave durable SQL data behind a
        // stale cache entry. A cache mutation failure is still surfaced as CaeriusCacheException by this strict
        // path, but caller cancellation can no longer interrupt it.
        await CaeriusExecutor.InvalidateStrictAsync(Context, invalidations, CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_state != Active)
                throw new InvalidOperationException("The transaction is no longer active.");
            if (_commandInFlight != 0)
                throw new InvalidOperationException("A command is still running on this transaction.");

            Volatile.Write(ref _state, Completing);
            _deferredInvalidations.Clear();
        }

        try
        {
            // Match CommitAsync's terminal-operation contract: honour a token only before the rollback starts.
            // Once the scope is reserved, rollback must complete even if request cancellation races its TDS
            // exchange; deferred invalidations were already abandoned under the transaction gate.
            await Transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (SqlException exception)
        {
            throw new CaeriusNetSqlException("<rollback-transaction>", exception);
        }
        finally
        {
            try
            {
                await Transaction.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await _lease.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    Volatile.Write(ref _state, Completed);
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_state != Active)
                return;
            if (_commandInFlight != 0)
                throw new InvalidOperationException("A command is still running on this transaction.");

            Volatile.Write(ref _state, Disposing);
            _deferredInvalidations.Clear();
        }

        try
        {
            await Transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (SqlException)
        {
        }
        finally
        {
            try
            {
                await Transaction.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await _lease.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    Volatile.Write(ref _state, Disposed);
                }
            }
        }
    }

    internal static async ValueTask<CaeriusNetTransaction> BeginAsync(
        ICaeriusExecutionContext context,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        var lease = await context.OpenLeaseAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = (SqlTransaction)await lease.Connection
                .BeginTransactionAsync(isolationLevel, cancellationToken)
                .ConfigureAwait(false);
            return new CaeriusNetTransaction(context, lease, transaction);
        }
        catch (OperationCanceledException)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        catch (SqlException exception)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            throw new CaeriusNetSqlException("<begin-transaction>", exception);
        }
        catch
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal void AcquireCommandSlot()
    {
        lock (_gate)
        {
            if (_state != Active)
                throw new InvalidOperationException("The transaction is no longer active.");
            if (Volatile.Read(ref _poisoned) != 0)
                throw new InvalidOperationException("The transaction is poisoned and must be rolled back.");
            if (_commandInFlight != 0)
                throw new InvalidOperationException("Concurrent commands are not allowed on a CaeriusNet transaction.");
            _commandInFlight = 1;
        }
    }

    internal void ReleaseCommandSlot()
    {
        lock (_gate)
        {
            _commandInFlight = 0;
        }
    }

    internal void Poison()
    {
        Volatile.Write(ref _poisoned, 1);
    }

    internal void DeferInvalidations(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        lock (_gate)
        {
            if (_state != Active || _commandInFlight == 0)
                throw new InvalidOperationException(
                    "Invalidations must be deferred while an active transaction command owns its slot.");
            foreach (var key in keys)
                _deferredInvalidations.Add(key);
        }
    }
}
