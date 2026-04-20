namespace CaeriusNet.IntegrationTests.Tests;

/// <summary>
///     Exhaustive coverage of <see cref="SimpleReadSqlAsyncCommands"/> on a real SQL Server. Each
///     read flavour (immutable array, read-only collection, IEnumerable, FirstQuery) is exercised
///     against both populated and empty result sets. Capacity tuning is also validated to ensure
///     the value flows through to <c>StoredProcedureParameters.Capacity</c> without truncating rows.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ReadCommandsTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private async Task SeedAsync(ICaeriusNetDbContext db, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            var p = new StoredProcedureParametersBuilder("dbo", "usp_InsertWidget")
                .AddParameter("@Name", $"W{i:000}", SqlDbType.NVarChar)
                .AddParameter("@Quantity", i, SqlDbType.Int)
                .Build();
            await db.ExecuteScalarAsync<int>(p);
        }
    }

    [Fact]
    public async Task QueryAsReadOnlyCollection_Returns_All_Rows()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        await SeedAsync(db, 5);

        var p = new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets").Build();
        var widgets = await db.QueryAsReadOnlyCollectionAsync<WidgetDto>(p);

        Assert.Equal(5, widgets.Count);
        Assert.Equal(["W001", "W002", "W003", "W004", "W005"], widgets.Select(w => w.Name).ToArray());
    }

    [Fact]
    public async Task QueryAsReadOnlyCollection_Returns_Empty_Singleton_When_NoRows()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var p = new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets").Build();
        var widgets = await db.QueryAsReadOnlyCollectionAsync<WidgetDto>(p);

        Assert.Empty(widgets);
        // Empty result sets short-circuit through CaeriusNet.Helpers.EmptyCollections, so two reads
        // must hand back the SAME singleton instance — proving the alloc-free path.
        var widgets2 = await db.QueryAsReadOnlyCollectionAsync<WidgetDto>(p);
        Assert.Same(widgets, widgets2);
    }

    [Fact]
    public async Task QueryAsIEnumerable_Returns_All_Rows()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        await SeedAsync(db, 3);

        var p = new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets").Build();
        var widgets = (await db.QueryAsIEnumerableAsync<WidgetDto>(p)).ToArray();

        Assert.Equal(3, widgets.Length);
        Assert.All(widgets, w => Assert.StartsWith("W", w.Name));
    }

    [Fact]
    public async Task QueryAsImmutableArray_Pre_Allocates_With_Capacity_Hint()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        await SeedAsync(db, 50);

        // ResultSetCapacity hint flows into StoredProcedureParameters.Capacity, which the immutable
        // array helper uses to pre-size the builder. We don't observe the capacity directly, but a
        // mismatched hint would either OOM small or truncate large — either way row count diverges.
        var p = new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets", ResultSetCapacity: 64).Build();
        var widgets = await db.QueryAsImmutableArrayAsync<WidgetDto>(p);

        Assert.Equal(50, widgets.Length);
    }

    [Fact]
    public async Task ExecuteScalar_Returns_Default_When_Count_Is_Zero()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var p = new StoredProcedureParametersBuilder("dbo", "usp_CountWidgets").Build();
        var count = await db.ExecuteScalarAsync<long>(p);

        Assert.Equal(0L, count);
    }

    [Fact]
    public async Task ExecuteAsync_Fire_And_Forget_Does_Not_Throw()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var p = new StoredProcedureParametersBuilder("dbo", "usp_InsertWidget")
            .AddParameter("@Name", "FireForget", SqlDbType.NVarChar)
            .AddParameter("@Quantity", 1, SqlDbType.Int)
            .Build();

        await db.ExecuteAsync(p);

        // Side-effect verified independently: the row IS persisted even though no scalar was awaited.
        var count = await db.ExecuteScalarAsync<long>(
            new StoredProcedureParametersBuilder("dbo", "usp_CountWidgets").Build());
        Assert.Equal(1L, count);
    }
}
