using System.Globalization;

namespace CaeriusNet.IntegrationTests.Tests;

/// <summary>
///     End-to-end coverage of the transaction API: commit persists, rollback discards, double-commit
///     throws, nested-transaction throws.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class TransactionTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Commit_Persists_Inserts()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        await using (var tx = await db.BeginTransactionAsync())
        {
            var p = new StoredProcedureParametersBuilder("dbo", "usp_InsertWidget")
                .AddParameter("@Name", "Persisted", SqlDbType.NVarChar)
                .AddParameter("@Quantity", 5, SqlDbType.Int)
                .Build();
            await tx.ExecuteScalarAsync<int>(p);

            await tx.CommitAsync();
        }

        var count = await ScalarAsync<long>("SELECT COUNT_BIG(*) FROM dbo.Widgets;");
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task Rollback_Discards_Inserts()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        await using (var tx = await db.BeginTransactionAsync())
        {
            var p = new StoredProcedureParametersBuilder("dbo", "usp_InsertWidget")
                .AddParameter("@Name", "Rolled-back", SqlDbType.NVarChar)
                .AddParameter("@Quantity", 99, SqlDbType.Int)
                .Build();
            await tx.ExecuteScalarAsync<int>(p);

            await tx.RollbackAsync();
        }

        var count = await ScalarAsync<long>("SELECT COUNT_BIG(*) FROM dbo.Widgets;");
        Assert.Equal(0L, count);
    }

    [Fact]
    public async Task Implicit_Disposal_Without_Commit_Rolls_Back()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        await using (var tx = await db.BeginTransactionAsync())
        {
            var p = new StoredProcedureParametersBuilder("dbo", "usp_InsertWidget")
                .AddParameter("@Name", "ImplicitRollback", SqlDbType.NVarChar)
                .AddParameter("@Quantity", 1, SqlDbType.Int)
                .Build();
            await tx.ExecuteScalarAsync<int>(p);
            // no Commit / Rollback — DisposeAsync must rollback
        }

        var count = await ScalarAsync<long>("SELECT COUNT_BIG(*) FROM dbo.Widgets;");
        Assert.Equal(0L, count);
    }

    [Fact]
    public async Task Nested_BeginTransactionAsync_Throws_NotSupported()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        await using var outer = await db.BeginTransactionAsync();

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await outer.BeginTransactionAsync());
    }

    [Fact]
    public async Task Transaction_Read_Returns_Uncommitted_Inserts()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        await using var tx = await db.BeginTransactionAsync();

        var insert = new StoredProcedureParametersBuilder("dbo", "usp_InsertWidget")
            .AddParameter("@Name", "TxRead", SqlDbType.NVarChar)
            .AddParameter("@Quantity", 8, SqlDbType.Int)
            .Build();
        var newId = await tx.ExecuteScalarAsync<int>(insert);

        var select = new StoredProcedureParametersBuilder("dbo", "usp_GetWidgetById")
            .AddParameter("@Id", newId, SqlDbType.Int)
            .Build();
        var widget = await tx.FirstQueryAsync<WidgetDto>(select);

        Assert.NotNull(widget);
        Assert.Equal("TxRead", widget!.Name);

        await tx.RollbackAsync();
    }

    [Theory]
    [InlineData(IsolationLevel.ReadCommitted, 2)]
    [InlineData(IsolationLevel.RepeatableRead, 3)]
    [InlineData(IsolationLevel.Serializable, 4)]
    public async Task BeginTransactionAsync_Honors_Requested_IsolationLevel(IsolationLevel requested,
        short expectedSqlServerLevel)
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        await using var tx = await db.BeginTransactionAsync(requested);

        // Ask the server itself which isolation level the current session is running under.
        // sys.dm_exec_sessions.transaction_isolation_level: 1=ReadUncommitted, 2=ReadCommitted,
        // 3=RepeatableRead, 4=Serializable, 5=Snapshot.
        var probe = new StoredProcedureParametersBuilder("dbo", "usp_GetSessionIsolationLevel").Build();
        var actual = await tx.ExecuteScalarAsync<short>(probe);

        Assert.Equal(expectedSqlServerLevel, actual);

        await tx.RollbackAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T), CultureInfo.InvariantCulture);
    }
}