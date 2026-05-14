namespace CaeriusNet.IntegrationTests.Tests;

/// <summary>
///     Live SQL exercises of the InMemory and Frozen cache pipelines wired through
///     <c>StoredProcedureParametersBuilder.AddInMemoryCache</c> / <c>AddFrozenCache</c> and the
///     <see cref="ICaeriusNetCache" /> invalidation façade. Each test uses a unique cache key so that
///     parallel test execution can't cross-pollute the static cache managers.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class CacheIntegrationTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private static string Key(string suffix)
    {
        return $"caerius-it-{Guid.NewGuid():N}-{suffix}";
    }

    private async Task<int> InsertAsync(ICaeriusNetDbContext db, string name, int qty)
    {
        var p = new StoredProcedureParametersBuilder("dbo", "usp_InsertWidget")
            .AddParameter("@Name", name, SqlDbType.NVarChar)
            .AddParameter("@Quantity", qty, SqlDbType.Int)
            .Build();
        return await db.ExecuteScalarAsync<int>(p);
    }

    [Fact]
    public async Task InMemory_Cache_Returns_Stale_Value_Until_Invalidated()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<ICaeriusNetCache>();
        var key = Key("inmem");

        await InsertAsync(db, "Cached1", 1);

        var first = await db.QueryAsImmutableArrayAsync<WidgetDto>(
            new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
                .AddInMemoryCache(key, TimeSpan.FromMinutes(5)).Build());
        Assert.Single(first);

        // Mutate the underlying table — a non-cached read would now see TWO rows.
        await InsertAsync(db, "Cached2", 2);

        var cachedHit = await db.QueryAsImmutableArrayAsync<WidgetDto>(
            new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
                .AddInMemoryCache(key, TimeSpan.FromMinutes(5)).Build());
        Assert.Single(cachedHit); // stale, served from cache
        Assert.Same(first[0], cachedHit[0]);

        await cache.RemoveAsync(key);

        var fresh = await db.QueryAsImmutableArrayAsync<WidgetDto>(
            new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
                .AddInMemoryCache(key, TimeSpan.FromMinutes(5)).Build());
        Assert.Equal(2, fresh.Length);
    }

    [Fact]
    public async Task Frozen_Cache_Is_Idempotent_For_Same_Key()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<ICaeriusNetCache>();
        var key = Key("frozen");

        await InsertAsync(db, "Frozen1", 1);

        var p = new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
            .AddFrozenCache(key).Build();
        var snap1 = await db.QueryAsImmutableArrayAsync<WidgetDto>(p);
        Assert.Single(snap1);

        await InsertAsync(db, "Frozen2", 2);
        var snap2 = await db.QueryAsImmutableArrayAsync<WidgetDto>(
            new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
                .AddFrozenCache(key).Build());

        // Frozen cache: same instance, never refreshed under the same key.
        Assert.Single(snap2);
        Assert.Same(snap1[0], snap2[0]);

        await cache.RemoveAsync(key, CacheType.Frozen);

        var snap3 = await db.QueryAsImmutableArrayAsync<WidgetDto>(
            new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
                .AddFrozenCache(key).Build());
        Assert.Equal(2, snap3.Length);
    }

    [Fact]
    public async Task Targeted_Remove_By_Type_Does_Not_Touch_Other_Tier()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<ICaeriusNetCache>();
        var memKey = Key("mem-only");
        var frozenKey = Key("frozen-only");

        await InsertAsync(db, "T1", 1);

        await db.QueryAsImmutableArrayAsync<WidgetDto>(
            new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
                .AddInMemoryCache(memKey, TimeSpan.FromMinutes(5)).Build());
        await db.QueryAsImmutableArrayAsync<WidgetDto>(
            new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
                .AddFrozenCache(frozenKey).Build());

        await InsertAsync(db, "T2", 2);

        // Remove only the InMemory entry — Frozen entry should remain stale (1 row).
        await cache.RemoveAsync(memKey, CacheType.InMemory);

        var memRefreshed = await db.QueryAsImmutableArrayAsync<WidgetDto>(
            new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
                .AddInMemoryCache(memKey, TimeSpan.FromMinutes(5)).Build());
        var frozenStillStale = await db.QueryAsImmutableArrayAsync<WidgetDto>(
            new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
                .AddFrozenCache(frozenKey).Build());

        Assert.Equal(2, memRefreshed.Length);
        Assert.Single(frozenStillStale);
    }

    [Fact]
    public async Task ClearAsync_Frozen_Wipes_All_Frozen_Entries()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<ICaeriusNetCache>();
        var k1 = Key("clr-1");
        var k2 = Key("clr-2");

        await InsertAsync(db, "C1", 1);

        await db.QueryAsImmutableArrayAsync<WidgetDto>(
            new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
                .AddFrozenCache(k1).Build());
        await db.QueryAsImmutableArrayAsync<WidgetDto>(
            new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
                .AddFrozenCache(k2).Build());

        await InsertAsync(db, "C2", 2);
        await cache.ClearAsync(CacheType.Frozen);

        var fresh1 = await db.QueryAsImmutableArrayAsync<WidgetDto>(
            new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
                .AddFrozenCache(k1).Build());
        var fresh2 = await db.QueryAsImmutableArrayAsync<WidgetDto>(
            new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets")
                .AddFrozenCache(k2).Build());

        Assert.Equal(2, fresh1.Length);
        Assert.Equal(2, fresh2.Length);
    }

    [Fact]
    public async Task ClearAsync_Redis_Throws_NotSupported()
    {
        using var scope = fixture.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICaeriusNetCache>();

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await cache.ClearAsync(CacheType.Redis));
    }

    [Fact]
    public async Task RemoveAsync_Empty_Key_Throws()
    {
        using var scope = fixture.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICaeriusNetCache>();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await cache.RemoveAsync(string.Empty));
    }
}
