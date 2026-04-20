namespace CaeriusNet.Factories;

/// <summary>
///     Default implementation of <see cref="ICaeriusNetTransaction" />. Owns its <see cref="SqlConnection" />
///     for the lifetime of the scope and exposes its <see cref="SqlTransaction" /> to internal command
///     extensions through <see cref="ICaeriusNetTransactionInternal" />.
/// </summary>
internal sealed class CaeriusNetTransaction : ICaeriusNetTransactionInternal
{
    private readonly ILogger? _logger;
    private readonly bool _isLoggingEnabled;

    private SqlConnection? _connection;
    private SqlTransaction? _transaction;
    private int _commandInFlight;        // 0 = none, 1 = busy
    private int _state;                  // 0 = active, 1 = committed, 2 = rolledback, 3 = poisoned, 4 = disposed

    private const int StateActive = 0;
    private const int StateCommitted = 1;
    private const int StateRolledBack = 2;
    private const int StatePoisoned = 3;
    private const int StateDisposed = 4;

    private CaeriusNetTransaction(SqlConnection connection, SqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
        _logger = LoggerProvider.GetLogger();
        _isLoggingEnabled = _logger != null;
    }

    public bool IsActive => Volatile.Read(ref _state) == StateActive;

    public SqlConnection Connection =>
        _connection ?? throw new InvalidOperationException("Transaction has been disposed.");

    public SqlTransaction Transaction =>
        _transaction ?? throw new InvalidOperationException("Transaction has been disposed.");

    /// <summary>
    ///     Opens a connection through <paramref name="dbContext" /> and begins a SQL Server transaction at the
    ///     requested isolation level. Cleans everything up if any step throws so we never leak a pooled
    ///     connection or a half-started transaction.
    /// </summary>
    internal static async ValueTask<ICaeriusNetTransaction> BeginAsync(
        ICaeriusNetDbContext dbContext,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        SqlConnection? connection = null;
        SqlTransaction? transaction = null;
        try
        {
            connection = await dbContext.DbConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (connection.State == ConnectionState.Closed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            transaction = (SqlTransaction)await connection
                .BeginTransactionAsync(isolationLevel, cancellationToken)
                .ConfigureAwait(false);

            var tx = new CaeriusNetTransaction(connection, transaction);
            if (tx._isLoggingEnabled) tx._logger!.LogTransactionStarted(isolationLevel);
            return tx;
        }
        catch (SqlException ex)
        {
            if (transaction is not null) await transaction.DisposeAsync().ConfigureAwait(false);
            if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
            throw new CaeriusNetSqlException("Failed to open SQL Server transaction.", ex);
        }
        catch
        {
            if (transaction is not null) await transaction.DisposeAsync().ConfigureAwait(false);
            if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        var prev = Interlocked.CompareExchange(ref _state, StateCommitted, StateActive);
        if (prev != StateActive)
            throw new InvalidOperationException(
                $"Cannot commit: transaction is in state '{StateName(prev)}'.");

        try
        {
            await Transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            if (_isLoggingEnabled) _logger!.LogTransactionCommitted();
        }
        catch (SqlException ex)
        {
            Volatile.Write(ref _state, StatePoisoned);
            throw new CaeriusNetSqlException("Failed to commit SQL Server transaction.", ex);
        }
    }

    public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        var current = Volatile.Read(ref _state);
        if (current is StateCommitted or StateRolledBack or StateDisposed)
            throw new InvalidOperationException(
                $"Cannot rollback: transaction is in state '{StateName(current)}'.");

        try
        {
            await Transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _state, StateRolledBack);
            if (_isLoggingEnabled) _logger!.LogTransactionRolledBack();
        }
        catch (SqlException ex)
        {
            Volatile.Write(ref _state, StatePoisoned);
            throw new CaeriusNetSqlException("Failed to rollback SQL Server transaction.", ex);
        }
    }

    public void AcquireCommandSlot()
    {
        var current = Volatile.Read(ref _state);
        if (current != StateActive)
            throw new InvalidOperationException(
                $"Cannot execute command: transaction is in state '{StateName(current)}'.");

        if (Interlocked.CompareExchange(ref _commandInFlight, 1, 0) != 0)
            throw new InvalidOperationException(
                "Concurrent commands are not supported on a single transaction scope. " +
                "SqlConnection is not thread-safe; await each command before issuing the next.");
    }

    public void ReleaseCommandSlot()
    {
        Volatile.Write(ref _commandInFlight, 0);
    }

    public void Poison()
    {
        Interlocked.CompareExchange(ref _state, StatePoisoned, StateActive);
    }

    public async ValueTask DisposeAsync()
    {
        var prev = Interlocked.Exchange(ref _state, StateDisposed);
        if (prev == StateDisposed) return;

        if (prev is StateActive or StatePoisoned && _transaction is not null)
        {
            try
            {
                await _transaction.RollbackAsync().ConfigureAwait(false);
                if (_isLoggingEnabled) _logger!.LogTransactionRolledBack();
            }
            catch
            {
                // Best-effort rollback during disposal; surface no exception to callers.
            }
        }

        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }

    private static string StateName(int state) => state switch
    {
        StateActive => "Active",
        StateCommitted => "Committed",
        StateRolledBack => "RolledBack",
        StatePoisoned => "Poisoned",
        StateDisposed => "Disposed",
        _ => "Unknown"
    };
}
