using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace CaeriusNet.Factories;

/// <summary>
///     Default implementation of <see cref="ICaeriusNetTransaction" />. Owns its <see cref="SqlConnection" />
///     for the lifetime of the scope and exposes its <see cref="SqlTransaction" /> to internal command
///     extensions through <see cref="ICaeriusNetTransactionInternal" />.
/// </summary>
internal sealed class CaeriusNetTransaction : ICaeriusNetTransactionInternal
{
    private const int StateActive = 0;
    private const int StateCommitted = 1;
    private const int StateRolledBack = 2;
    private const int StatePoisoned = 3;
    private const int StateDisposed = 4;
    private readonly bool _isLoggingEnabled;
    private readonly ILogger? _logger;
    private int _commandInFlight; // 0 = none, 1 = busy
    private SqlConnection? _connection;
    private int _state; // 0 = active, 1 = committed, 2 = rolledback, 3 = poisoned, 4 = disposed
    private SqlTransaction? _transaction;

    private Activity? _txActivity;

    private CaeriusNetTransaction(SqlConnection connection, SqlTransaction transaction, Activity? txActivity)
    {
        _connection = connection;
        _transaction = transaction;
        _txActivity = txActivity;
        _logger = LoggerProvider.GetLogger();
        _isLoggingEnabled = _logger != null;
    }

    public bool IsActive => Volatile.Read(ref _state) == StateActive;

    public SqlConnection Connection =>
        _connection ?? throw new InvalidOperationException("Transaction has been disposed.");

    public SqlTransaction Transaction =>
        _transaction ?? throw new InvalidOperationException("Transaction has been disposed.");

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        AcquireCompletionSlot("commit");

        try
        {
            var current = Volatile.Read(ref _state);
            if (current != StateActive)
                throw new InvalidOperationException(
                    $"Cannot commit: transaction is in state '{StateName(current)}'.");

            try
            {
                await Transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException ex)
            {
                MarkCompletionFailure("commit-failed");
                throw new CaeriusNetSqlException("Failed to commit SQL Server transaction.", ex);
            }
            catch (Exception ex) when (IsTransactionLifecycleFailure(ex))
            {
                MarkCompletionFailure("commit-failed");
                throw;
            }

            Volatile.Write(ref _state, StateCommitted);
            CaeriusActivityExtensions.RecordTransactionOutcome(_txActivity, "committed");
            _txActivity = null;
            if (_isLoggingEnabled) _logger!.LogTransactionCommitted();
        }
        finally
        {
            ReleaseCommandSlot();
        }
    }

    public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        AcquireCompletionSlot("rollback");

        try
        {
            var current = Volatile.Read(ref _state);
            if (current is StateCommitted or StateRolledBack or StateDisposed)
                throw new InvalidOperationException(
                    $"Cannot rollback: transaction is in state '{StateName(current)}'.");

            try
            {
                await Transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException ex)
            {
                MarkCompletionFailure("rollback-failed");
                throw new CaeriusNetSqlException("Failed to rollback SQL Server transaction.", ex);
            }
            catch (Exception ex) when (IsTransactionLifecycleFailure(ex))
            {
                MarkCompletionFailure("rollback-failed");
                throw;
            }

            Volatile.Write(ref _state, StateRolledBack);
            CaeriusActivityExtensions.RecordTransactionOutcome(_txActivity, "rolled-back");
            _txActivity = null;
            if (_isLoggingEnabled) _logger!.LogTransactionRolledBack();
        }
        finally
        {
            ReleaseCommandSlot();
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
        if (Interlocked.CompareExchange(ref _state, StatePoisoned, StateActive) == StateActive
            && _isLoggingEnabled)
            _logger!.LogTransactionPoisoned();
    }

    public async ValueTask DisposeAsync()
    {
        if (Volatile.Read(ref _state) == StateDisposed)
            return;

        AcquireCompletionSlot("dispose");

        try
        {
            var prev = Interlocked.Exchange(ref _state, StateDisposed);
            if (prev == StateDisposed) return;

            var transaction = _transaction;
            if ((prev is StateActive or StatePoisoned) && transaction is not null)
                try
                {
                    await transaction.RollbackAsync().ConfigureAwait(false);
                    var outcome = prev == StatePoisoned ? "poisoned-auto-rollback" : "auto-rollback";
                    CaeriusActivityExtensions.RecordTransactionOutcome(_txActivity, outcome, prev == StatePoisoned);
                    _txActivity = null;
                    if (_isLoggingEnabled) _logger!.LogTransactionRolledBack();
                }
                catch
                {
                    // Best-effort rollback during disposal; surface no rollback exception to callers.
                    _txActivity?.Stop();
                    _txActivity = null;
                }

            await DisposeOwnedResourcesAsync(suppressExceptions: false).ConfigureAwait(false);
        }
        finally
        {
            ReleaseCommandSlot();
        }
    }

    private void AcquireCompletionSlot(string operation)
    {
        if (Interlocked.CompareExchange(ref _commandInFlight, 1, 0) != 0)
            throw new InvalidOperationException(
                $"Cannot {operation}: a command is already in flight on this transaction scope.");
    }

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
            if (connection.State != ConnectionState.Open)
                throw new InvalidOperationException("DbConnectionAsync returned a non-open SQL connection.");

            transaction = (SqlTransaction)await connection
                .BeginTransactionAsync(isolationLevel, cancellationToken)
                .ConfigureAwait(false);

            // Start the parent TX activity AFTER the connection is open and the transaction is
            // begun. All SP activities started inside this scope will become children of this
            // activity because Activity.Current is set to it on the calling async context.
            var txActivity = CaeriusActivityExtensions.StartTransactionActivity(isolationLevel);

            var tx = new CaeriusNetTransaction(connection, transaction, txActivity);
            if (tx._isLoggingEnabled) tx._logger!.LogTransactionStarted(isolationLevel);
            return tx;
        }
        catch (SqlException ex)
        {
            await DisposeResourcesAsync(transaction, connection, suppressExceptions: true).ConfigureAwait(false);
            throw new CaeriusNetSqlException("Failed to open SQL Server transaction.", ex);
        }
        catch
        {
            await DisposeResourcesAsync(transaction, connection, suppressExceptions: true).ConfigureAwait(false);
            throw;
        }
    }

    private void MarkCompletionFailure(string outcome)
    {
        Volatile.Write(ref _state, StatePoisoned);
        CaeriusActivityExtensions.RecordTransactionOutcome(_txActivity, outcome, true);
        _txActivity = null;
    }

    private static bool IsTransactionLifecycleFailure(Exception ex)
    {
        return ex is InvalidOperationException or OperationCanceledException;
    }

    private async ValueTask DisposeOwnedResourcesAsync(bool suppressExceptions)
    {
        var transaction = _transaction;
        var connection = _connection;
        _transaction = null;
        _connection = null;

        await DisposeResourcesAsync(transaction, connection, suppressExceptions).ConfigureAwait(false);
    }

    private static async ValueTask DisposeResourcesAsync(
        SqlTransaction? transaction,
        SqlConnection? connection,
        bool suppressExceptions)
    {
        Exception? failure = null;

        if (transaction is not null)
            try
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception) when (suppressExceptions)
            {
            }
            catch (Exception ex)
            {
                failure = ex;
            }

        if (connection is not null)
            try
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception) when (suppressExceptions)
            {
            }
            catch (Exception ex)
            {
                failure ??= ex;
            }

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static string StateName(int state)
    {
        return state switch
        {
            StateActive => "Active",
            StateCommitted => "Committed",
            StateRolledBack => "RolledBack",
            StatePoisoned => "Poisoned",
            StateDisposed => "Disposed",
            _ => "Unknown"
        };
    }
}
