namespace CaeriusNet.Tests.Transactions;

/// <summary>
///     Unit tests for the <see cref="CaeriusNetTransaction" /> state machine — exercised without a real
///     SQL Server. These tests cover the failure paths (factory throws, nested transactions, illegal
///     state transitions). Happy-path execution against a real database lives in the integration test
///     project.
/// </summary>
public sealed class CaeriusNetTransactionTests
{
    [Fact]
    public async Task BeginTransactionAsync_NullDbContext_Throws()
    {
        ICaeriusNetDbContext dbContext = null!;
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            dbContext.BeginTransactionAsync().AsTask());
    }

    [Fact]
    public async Task BeginTransactionAsync_FactoryThrows_PropagatesAsCaeriusNetSqlException()
    {
        var dbContext = new ThrowingDbContext();

        await Assert.ThrowsAsync<CaeriusNetSqlException>(() =>
            dbContext.BeginTransactionAsync().AsTask());
    }

    [Fact]
    public async Task NestedTransactionRejection_Throws_NotSupported()
    {
        var fakeTx = new FakeTransaction();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            fakeTx.BeginTransactionAsync().AsTask());
    }

    [Fact]
    public async Task BeginTransactionAsync_ConnectionFails_DisposesConnection()
    {
        var dbContext = new FailToOpenDbContext();

        await Assert.ThrowsAsync<CaeriusNetSqlException>(() =>
            dbContext.BeginTransactionAsync().AsTask());

        Assert.Equal(1, dbContext.ConnectionsCreated);
    }

    [Fact]
    public async Task FailToOpenDbContext_WrapsAsCaeriusNetSqlException()
    {
        var dbContext = new FailToOpenDbContext();

        var ex = await Assert.ThrowsAsync<CaeriusNetSqlException>(() =>
            dbContext.BeginTransactionAsync().AsTask());

        Assert.IsType<CaeriusNetSqlException>(ex);
    }

    [Fact]
    public void FakeTransaction_IsActive_ReturnsTrue()
    {
        var fakeTx = new FakeTransaction();

        Assert.True(fakeTx.IsActive);
    }

    [Fact]
    public async Task FakeTransaction_CommitAsync_DoesNotThrow()
    {
        var fakeTx = new FakeTransaction();

        var ex = await Record.ExceptionAsync(() => fakeTx.CommitAsync().AsTask());

        Assert.Null(ex);
    }

    [Fact]
    public async Task BeginTransactionAsync_NullReference_Throws_ArgumentNullException()
    {
        ICaeriusNetDbContext dbContext = null!;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            dbContext.BeginTransactionAsync().AsTask());

        Assert.Equal("dbContext", ex.ParamName);
    }

    [Fact]
    public async Task CommitAsync_With_Command_In_Flight_Throws_Deterministically()
    {
        var tx = CreateStateMachineOnlyTransaction();
        tx.AcquireCommandSlot();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync().AsTask());

        Assert.Contains("command is already in flight", ex.Message);
        tx.ReleaseCommandSlot();
    }

    [Fact]
    public async Task RollbackAsync_With_Command_In_Flight_Throws_Deterministically()
    {
        var tx = CreateStateMachineOnlyTransaction();
        tx.AcquireCommandSlot();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tx.RollbackAsync().AsTask());

        Assert.Contains("command is already in flight", ex.Message);
        tx.ReleaseCommandSlot();
    }

    [Fact]
    public async Task DisposeAsync_With_Command_In_Flight_Throws_Deterministically()
    {
        var tx = CreateStateMachineOnlyTransaction();
        tx.AcquireCommandSlot();

        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tx.DisposeAsync().AsTask());

            Assert.Contains("command is already in flight", ex.Message);
        }
        finally
        {
            tx.ReleaseCommandSlot();
            await tx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CommitAsync_When_Lifecycle_Fails_Poisons_Instead_Of_Marking_Committed()
    {
        var tx = CreateStateMachineOnlyTransaction();

        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync().AsTask());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync().AsTask());
        Assert.Contains("Poisoned", ex.Message);

        await tx.DisposeAsync();
    }

    [Fact]
    public async Task RollbackAsync_When_Lifecycle_Fails_Poisons_Transaction()
    {
        var tx = CreateStateMachineOnlyTransaction();

        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.RollbackAsync().AsTask());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tx.CommitAsync().AsTask());
        Assert.Contains("Poisoned", ex.Message);

        await tx.DisposeAsync();
    }

    [Fact]
    public async Task TransactionWriteCommand_InvalidOperation_Poisons_And_Releases_Slot()
    {
        var failure = new InvalidOperationException("Connection is not usable.");
        var tx = new FailingInternalTransaction(failure);
        var parameters = CreateStoredProcedureParameters();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tx.ExecuteNonQueryAsync(parameters).AsTask());

        Assert.Same(failure, ex);
        Assert.True(tx.Poisoned);
        Assert.Equal(1, tx.AcquireCount);
        Assert.Equal(1, tx.ReleaseCount);
    }

    [Fact]
    public async Task TransactionWriteCommand_Cancellation_Poisons_And_Releases_Slot()
    {
        var failure = new OperationCanceledException("Command canceled.");
        var tx = new FailingInternalTransaction(failure);
        var parameters = CreateStoredProcedureParameters();

        var ex = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            tx.ExecuteAsync(parameters).AsTask());

        Assert.Same(failure, ex);
        Assert.True(tx.Poisoned);
        Assert.Equal(1, tx.AcquireCount);
        Assert.Equal(1, tx.ReleaseCount);
    }

    [Fact]
    public async Task TransactionReadCommand_InvalidOperation_Poisons_And_Releases_Slot()
    {
        var failure = new InvalidOperationException("Connection is not usable.");
        var tx = new FailingInternalTransaction(failure);
        var parameters = CreateStoredProcedureParameters();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tx.FirstQueryAsync<TestMapper>(parameters).AsTask());

        Assert.Same(failure, ex);
        Assert.True(tx.Poisoned);
        Assert.Equal(1, tx.AcquireCount);
        Assert.Equal(1, tx.ReleaseCount);
    }

    private static StoredProcedureParameters CreateStoredProcedureParameters()
    {
        return new StoredProcedureParametersBuilder("dbo", "sp_Test").Build();
    }

    private static ICaeriusNetTransactionInternal CreateStateMachineOnlyTransaction()
    {
        var constructor = typeof(CaeriusNetTransaction).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        var connection = new SqlConnection();
        var inner = (ICaeriusNetTransactionInternal)constructor.Invoke([connection, null, null]);
        return new OwnedConnectionTransaction(inner, connection);
    }

    private sealed class OwnedConnectionTransaction : ICaeriusNetTransactionInternal
    {
        private readonly SqlConnection _connection;
        private readonly ICaeriusNetTransactionInternal _inner;

        public OwnedConnectionTransaction(ICaeriusNetTransactionInternal inner, SqlConnection connection)
        {
            _inner = inner;
            _connection = connection;
        }

        public bool IsActive => _inner.IsActive;

        public SqlConnection Connection => _inner.Connection;

        public SqlTransaction Transaction => _inner.Transaction;

        public void AcquireCommandSlot()
        {
            _inner.AcquireCommandSlot();
        }

        public void ReleaseCommandSlot()
        {
            _inner.ReleaseCommandSlot();
        }

        public void Poison()
        {
            _inner.Poison();
        }

        public ValueTask CommitAsync(CancellationToken cancellationToken = default)
        {
            return _inner.CommitAsync(cancellationToken);
        }

        public ValueTask RollbackAsync(CancellationToken cancellationToken = default)
        {
            return _inner.RollbackAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            _connection.Dispose();
        }
    }

    private sealed class FakeTransaction : ICaeriusNetTransaction
    {
        public bool IsActive => true;

        public ValueTask CommitAsync(CancellationToken cancellationToken = default)
        {
            return default;
        }

        public ValueTask RollbackAsync(CancellationToken cancellationToken = default)
        {
            return default;
        }

        public ValueTask DisposeAsync()
        {
            return default;
        }
    }

    private sealed class FailingInternalTransaction(Exception failure) : ICaeriusNetTransactionInternal
    {
        public int AcquireCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public bool Poisoned { get; private set; }
        public bool IsActive => !Poisoned;
        public SqlConnection Connection => throw failure;
        public SqlTransaction Transaction => throw failure;

        public void AcquireCommandSlot()
        {
            AcquireCount++;
        }

        public void ReleaseCommandSlot()
        {
            ReleaseCount++;
        }

        public void Poison()
        {
            Poisoned = true;
        }

        public ValueTask CommitAsync(CancellationToken cancellationToken = default)
        {
            return default;
        }

        public ValueTask RollbackAsync(CancellationToken cancellationToken = default)
        {
            return default;
        }

        public ValueTask DisposeAsync()
        {
            return default;
        }
    }

    private sealed class TestMapper : ISpMapper<TestMapper>
    {
        public static TestMapper MapFromDataReader(SqlDataReader reader)
        {
            return new TestMapper();
        }
    }

    private sealed class ThrowingDbContext : ICaeriusNetDbContext
    {
        public IRedisCacheManager? RedisCacheManager => null;

        public ValueTask<SqlConnection> DbConnectionAsync(CancellationToken cancellationToken = default)
        {
            throw SqlExceptionFactory.Create("Simulated factory failure.");
        }
    }

    private sealed class FailToOpenDbContext : ICaeriusNetDbContext
    {
        public int ConnectionsCreated { get; private set; }
        public IRedisCacheManager? RedisCacheManager => null;

        public ValueTask<SqlConnection> DbConnectionAsync(CancellationToken cancellationToken = default)
        {
            ConnectionsCreated++;
            return new ValueTask<SqlConnection>(new SqlConnection(
                "Server=tcp:127.0.0.1,1;Database=__caerius_unit_tests__;Connect Timeout=1;Encrypt=False;TrustServerCertificate=True"));
        }
    }
}
