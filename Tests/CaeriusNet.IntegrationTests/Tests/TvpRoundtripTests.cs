namespace CaeriusNet.IntegrationTests.Tests;

/// <summary>
///     Validates the source-generated TVP mapper round-trips correctly through SQL Server.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class TvpRoundtripTests(SqlServerFixture fixture) : IAsyncLifetime
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
    public async Task BulkInsert_Via_Tvp_Materialises_All_Rows()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var items = new[]
        {
            new WidgetTvp("Widget-1", 10),
            new WidgetTvp("Widget-2", 20),
            new WidgetTvp("Widget-3", 30)
        };

        var parameters = new StoredProcedureParametersBuilder("dbo", "usp_BulkInsertWidgets")
            .AddTvpParameter("@Items", items)
            .Build();

        var inserted = await db.ExecuteScalarAsync<int>(parameters);

        Assert.Equal(items.Length, inserted);

        var listParams = new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets").Build();
        var roundTripped = await db.QueryAsImmutableArrayAsync<WidgetDto>(listParams);

        Assert.Equal(items.Length, roundTripped.Length);
        Assert.Equal(items.Select(i => i.Name).ToArray(), roundTripped.Select(w => w.Name).ToArray());
        Assert.Equal(items.Select(i => i.Quantity).ToArray(), roundTripped.Select(w => w.Quantity).ToArray());
    }

    [Fact]
    public async Task Empty_Tvp_Is_Rejected_By_Builder()
    {
        Assert.Throws<ArgumentException>(() =>
            new StoredProcedureParametersBuilder("dbo", "usp_BulkInsertWidgets")
                .AddTvpParameter("@Items", Array.Empty<WidgetTvp>()));

        await Task.CompletedTask;
    }

    [Fact]
    public async Task BulkInsert_Via_Tvp_In_Transaction_Commits()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var items = new[]
        {
            new WidgetTvp("Committed-1", 100),
            new WidgetTvp("Committed-2", 200)
        };

        await using (var tx = await db.BeginTransactionAsync())
        {
            var parameters = new StoredProcedureParametersBuilder("dbo", "usp_BulkInsertWidgets")
                .AddTvpParameter("@Items", items)
                .Build();

            var inserted = await tx.ExecuteScalarAsync<int>(parameters);
            Assert.Equal(items.Length, inserted);

            await tx.CommitAsync();
        }

        var listParams = new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets").Build();
        var widgets = await db.QueryAsImmutableArrayAsync<WidgetDto>(listParams);

        Assert.Equal(items.Length, widgets.Length);
        Assert.Equal(items.Select(item => item.Name).ToArray(), widgets.Select(widget => widget.Name).ToArray());
    }

    [Fact]
    public async Task BulkInsert_Via_Tvp_In_Transaction_Rollback_Discards()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var items = new[]
        {
            new WidgetTvp("RolledBack-1", 100),
            new WidgetTvp("RolledBack-2", 200)
        };

        await using (var tx = await db.BeginTransactionAsync())
        {
            var parameters = new StoredProcedureParametersBuilder("dbo", "usp_BulkInsertWidgets")
                .AddTvpParameter("@Items", items)
                .Build();

            var inserted = await tx.ExecuteScalarAsync<int>(parameters);
            Assert.Equal(items.Length, inserted);

            await tx.RollbackAsync();
        }

        var listParams = new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets").Build();
        var widgets = await db.QueryAsImmutableArrayAsync<WidgetDto>(listParams);

        Assert.Empty(widgets);
    }
}
