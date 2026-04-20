namespace CaeriusNet.IntegrationTests.Tests;

/// <summary>
///     End-to-end coverage of the multi-result-set helpers (immutable array, read-only collection,
///     enumerable). Validates that two- and three-result-set sprocs are correctly tuple-unwrapped,
///     and that empty trailing result sets don't break the readers.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class MultiResultSetTests(SqlServerFixture fixture) : IAsyncLifetime
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
                .AddParameter("@Name", $"M{i:000}", SqlDbType.NVarChar)
                .AddParameter("@Quantity", i * 10, SqlDbType.Int)
                .Build();
            await db.ExecuteScalarAsync<int>(p);
        }
    }

    [Fact]
    public async Task QueryMultipleImmutableArray_Returns_Two_Result_Sets()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        await SeedAsync(db, 4);

        var p = new StoredProcedureParametersBuilder("dbo", "usp_GetWidgetsAndCount").Build();
        var (widgets, counts) = await db.QueryMultipleImmutableArrayAsync<WidgetDto, WidgetCountDto>(p);

        Assert.Equal(4, widgets.Length);
        Assert.Single(counts);
        Assert.Equal(4L, counts[0].Total);
    }

    [Fact]
    public async Task QueryMultipleImmutableArray_Returns_Three_Result_Sets()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        await SeedAsync(db, 2);

        var p = new StoredProcedureParametersBuilder("dbo", "usp_GetWidgetsCountAndFirst").Build();
        var (all, count, first) =
            await db.QueryMultipleImmutableArrayAsync<WidgetDto, WidgetCountDto, WidgetDto>(p);

        Assert.Equal(2, all.Length);
        Assert.Equal(2L, count[0].Total);
        Assert.Single(first);
        Assert.Equal("M001", first[0].Name);
    }

    [Fact]
    public async Task QueryMultipleReadOnlyCollection_Returns_Two_Result_Sets()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        await SeedAsync(db, 3);

        var p = new StoredProcedureParametersBuilder("dbo", "usp_GetWidgetsAndCount").Build();
        var (widgets, counts) =
            await db.QueryMultipleReadOnlyCollectionAsync<WidgetDto, WidgetCountDto>(p);

        Assert.Equal(3, widgets.Count);
        Assert.Equal(3L, counts[0].Total);
    }

    [Fact]
    public async Task QueryMultipleIEnumerable_Returns_Two_Result_Sets()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        await SeedAsync(db, 5);

        var p = new StoredProcedureParametersBuilder("dbo", "usp_GetWidgetsAndCount").Build();
        var (widgetsRaw, countsRaw) =
            await db.QueryMultipleIEnumerableAsync<WidgetDto, WidgetCountDto>(p);

        var widgets = widgetsRaw.ToArray();
        var counts = countsRaw.ToArray();
        Assert.Equal(5, widgets.Length);
        Assert.Equal(5L, counts[0].Total);
    }

    [Fact]
    public async Task QueryMultipleImmutableArray_Empty_Tail_Returns_Empty_Array()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        // No seed — the first result set is empty, the count returns 0.
        var p = new StoredProcedureParametersBuilder("dbo", "usp_GetWidgetsAndCount").Build();
        var (widgets, counts) = await db.QueryMultipleImmutableArrayAsync<WidgetDto, WidgetCountDto>(p);

        Assert.Empty(widgets);
        Assert.Single(counts);
        Assert.Equal(0L, counts[0].Total);
    }
}
