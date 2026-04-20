namespace CaeriusNet.IntegrationTests.Tests;

/// <summary>
///     End-to-end coverage of the simple stored-procedure read/write surface against a real SQL Server.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class StoredProcedureTests(SqlServerFixture fixture) : IAsyncLifetime
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
    public async Task ExecuteScalarAsync_Inserts_Row_And_Returns_Identity()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var parameters = new StoredProcedureParametersBuilder("dbo", "usp_InsertWidget")
            .AddParameter("@Name", "Alpha", SqlDbType.NVarChar)
            .AddParameter("@Quantity", 10, SqlDbType.Int)
            .Build();

        var newId = await db.ExecuteScalarAsync<int>(parameters);

        Assert.Equal(1, newId);
    }

    [Fact]
    public async Task FirstQueryAsync_Returns_Mapped_Dto()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var insertParams = new StoredProcedureParametersBuilder("dbo", "usp_InsertWidget")
            .AddParameter("@Name", "Bravo", SqlDbType.NVarChar)
            .AddParameter("@Quantity", 42, SqlDbType.Int)
            .Build();
        var newId = await db.ExecuteScalarAsync<int>(insertParams);

        var selectParams = new StoredProcedureParametersBuilder("dbo", "usp_GetWidgetById")
            .AddParameter("@Id", newId, SqlDbType.Int)
            .Build();
        var widget = await db.FirstQueryAsync<WidgetDto>(selectParams);

        Assert.NotNull(widget);
        Assert.Equal(newId, widget!.Id);
        Assert.Equal("Bravo", widget.Name);
        Assert.Equal(42, widget.Quantity);
    }

    [Fact]
    public async Task QueryAsImmutableArray_Returns_All_Rows_Ordered()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        foreach (var (name, qty) in new[] { ("A", 1), ("B", 2), ("C", 3) })
        {
            var p = new StoredProcedureParametersBuilder("dbo", "usp_InsertWidget")
                .AddParameter("@Name", name, SqlDbType.NVarChar)
                .AddParameter("@Quantity", qty, SqlDbType.Int)
                .Build();
            await db.ExecuteScalarAsync<int>(p);
        }

        var listParams = new StoredProcedureParametersBuilder("dbo", "usp_ListWidgets").Build();
        var widgets = await db.QueryAsImmutableArrayAsync<WidgetDto>(listParams);

        Assert.Equal(3, widgets.Length);
        Assert.Equal(["A", "B", "C"], widgets.Select(w => w.Name).ToArray());
    }

    [Fact]
    public async Task FirstQueryAsync_Returns_Null_When_NoRow()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var p = new StoredProcedureParametersBuilder("dbo", "usp_GetWidgetById")
            .AddParameter("@Id", 9999, SqlDbType.Int)
            .Build();

        var widget = await db.FirstQueryAsync<WidgetDto>(p);

        Assert.Null(widget);
    }

    [Fact]
    public async Task Cancellation_Propagates_To_SqlClient()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        var p = new StoredProcedureParametersBuilder("dbo", "usp_LongRunning", CommandTimeout: 30)
            .AddParameter("@Seconds", 10, SqlDbType.Int)
            .Build();

        // Microsoft.Data.SqlClient may surface a cancelled query either as a raw
        // OperationCanceledException (token observed before/during ADO.NET I/O) or wrap a
        // SqlException, which CaeriusNet then re-wraps as CaeriusNetSqlException. Both are
        // valid contracts for "the request was cancelled".
        var exception = await Record.ExceptionAsync(async () =>
            await db.ExecuteScalarAsync<int>(p, cts.Token));

        Assert.NotNull(exception);
        Assert.True(
            exception is OperationCanceledException or CaeriusNetSqlException,
            $"Expected OperationCanceledException or CaeriusNetSqlException, got {exception!.GetType().FullName}: {exception.Message}");
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_Deletes_Row()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var insertParams = new StoredProcedureParametersBuilder("dbo", "usp_InsertWidget")
            .AddParameter("@Name", "Echo", SqlDbType.NVarChar)
            .AddParameter("@Quantity", 7, SqlDbType.Int)
            .Build();
        var id = await db.ExecuteScalarAsync<int>(insertParams);

        var deleteParams = new StoredProcedureParametersBuilder("dbo", "usp_DeleteWidget")
            .AddParameter("@Id", id, SqlDbType.Int)
            .Build();
        await db.ExecuteNonQueryAsync(deleteParams);

        var selectParams = new StoredProcedureParametersBuilder("dbo", "usp_GetWidgetById")
            .AddParameter("@Id", id, SqlDbType.Int)
            .Build();
        var widget = await db.FirstQueryAsync<WidgetDto>(selectParams);

        Assert.Null(widget);
    }
}