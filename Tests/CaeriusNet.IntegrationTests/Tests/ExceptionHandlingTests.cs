namespace CaeriusNet.IntegrationTests.Tests;

/// <summary>
///     Verifies SQL-side errors (RAISERROR, missing object, primary-key violation) are surfaced
///     end-to-end as <see cref="CaeriusNetSqlException"/> with the original <see cref="SqlException"/>
///     preserved as the inner exception. This is the contract the Src/ catch sites all uphold.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ExceptionHandlingTests(SqlServerFixture fixture) : IAsyncLifetime
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
    public async Task RaiseError_Wraps_SqlException()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var p = new StoredProcedureParametersBuilder("dbo", "usp_RaiseTestError").Build();

        var ex = await Assert.ThrowsAsync<CaeriusNetSqlException>(async () =>
            await db.ExecuteScalarAsync<int>(p));

        Assert.IsType<SqlException>(ex.InnerException);
        Assert.Contains("usp_RaiseTestError", ex.Message);
    }

    [Fact]
    public async Task Missing_Sproc_Wraps_SqlException()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var p = new StoredProcedureParametersBuilder("dbo", "usp_DoesNotExist_Caerius").Build();

        var ex = await Assert.ThrowsAsync<CaeriusNetSqlException>(async () =>
            await db.ExecuteScalarAsync<int>(p));

        Assert.IsType<SqlException>(ex.InnerException);
    }

    [Fact]
    public async Task RaiseError_In_Transaction_Poisons_The_Transaction()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        await using var tx = await db.BeginTransactionAsync();

        var bad = new StoredProcedureParametersBuilder("dbo", "usp_RaiseTestError").Build();
        await Assert.ThrowsAsync<CaeriusNetSqlException>(async () =>
            await tx.ExecuteScalarAsync<int>(bad));

        // Transaction is now poisoned — committing must throw, the connection is unsafe to reuse.
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await tx.CommitAsync());
    }

    [Fact]
    public async Task Read_From_Missing_Sproc_Wraps_SqlException()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var p = new StoredProcedureParametersBuilder("dbo", "usp_StillNotExists_Caerius").Build();

        var ex = await Assert.ThrowsAsync<CaeriusNetSqlException>(async () =>
            await db.QueryAsImmutableArrayAsync<WidgetDto>(p));

        Assert.IsType<SqlException>(ex.InnerException);
    }
}
