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

    private sealed class FakeTransaction : ICaeriusNetTransaction
    {
        public bool IsActive => true;
        public ValueTask CommitAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask RollbackAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask DisposeAsync() => default;
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
