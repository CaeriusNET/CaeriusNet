using Microsoft.Extensions.DependencyInjection;
using CaeriusNet.Execution;
using CaeriusNet.IntegrationTests.Infrastructure;
using CaeriusNet.Mappers;

namespace CaeriusNet.IntegrationTests.SqlServer;

public sealed class SqlServerStoredProcedureAcceptanceTests
{
    [Theory]
    [MemberData(nameof(ContainerTestGate.SqlServerImages), MemberType = typeof(ContainerTestGate))]
    public async Task StoredProcedureQueryAndEmptySmallAndLargeTvpsWorkOnSupportedSqlServerImages(
        string image)
    {
        ContainerTestGate.RequireEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var container = new MsSqlBuilder(image)
            .WithPassword("Caerius!Tests_2026")
            .Build();
        await container.StartAsync(cancellationToken);

        var connectionString = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            TrustServerCertificate = true
        }.ConnectionString;
        await ProvisionAsync(connectionString, cancellationToken);

        var services = new ServiceCollection();
        services.AddCaeriusNet(options => options.UseSqlServer(connectionString));
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var widget = await database.QuerySingleAsync<Widget>(
            new StoredProcedureCommandBuilder("dbo", "caerius_test_widget_by_id")
                .AddParameter("@id", 7, SqlDbType.Int)
                .Build(), cancellationToken);

        Assert.Equal(7, widget.Id);
        Assert.Equal("test widget", widget.Name);

        foreach (var expectedCount in new[] { 0, 1, 1_000 })
        {
            var rows = Enumerable.Range(1, expectedCount).Select(static value => new IntRow(value));
            var command = new StoredProcedureCommandBuilder("dbo", "caerius_test_count_ints")
                .AddTvpParameter("@values", rows)
                .Build();

            Assert.Equal(expectedCount, await database.ExecuteScalarAsync<int>(command, cancellationToken));
        }

        var unavailableRedisServices = new ServiceCollection();
        unavailableRedisServices.AddCaeriusNet(options => options.UseSqlServer(connectionString));
        unavailableRedisServices.AddCaeriusGeneratedCacheCodecs<Widget, WidgetCodec>();
        unavailableRedisServices.AddSingleton<ICaeriusRemoteCacheManager, FailingRemoteCache>();
        await using var unavailableRedisProvider = unavailableRedisServices.BuildServiceProvider();
        await using var unavailableRedisScope = unavailableRedisProvider.CreateAsyncScope();
        var unavailableRedisDatabase = unavailableRedisScope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        var readThroughCommand = new StoredProcedureCommandBuilder("dbo", "caerius_test_widget_by_id")
            .AddParameter("@id", 7, SqlDbType.Int)
            .UseRedisCache(TimeSpan.FromMinutes(1))
            .Build();
        var readThroughValue = await unavailableRedisDatabase.QueryImmutableArrayAsync<Widget>(
            readThroughCommand,
            cancellationToken: cancellationToken);
        Assert.Equal(widget, Assert.Single(readThroughValue));

        var strictInvalidation = new StoredProcedureCommandBuilder("dbo", "caerius_test_noop")
            .Invalidate("widget:7")
            .Build();
        await Assert.ThrowsAsync<CaeriusCacheException>(async () =>
            await unavailableRedisDatabase.ExecuteAsync(strictInvalidation, cancellationToken));
    }

    private static async Task ProvisionAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
                                       IF OBJECT_ID(N'dbo.CaeriusWidgets', N'U') IS NULL
                                           CREATE TABLE dbo.CaeriusWidgets (Id int NOT NULL PRIMARY KEY, Name nvarchar(80) NOT NULL);
                                       DELETE FROM dbo.CaeriusWidgets;
                                       INSERT dbo.CaeriusWidgets (Id, Name) VALUES (7, N'test widget');
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       IF TYPE_ID(N'dbo.CaeriusTestIntList') IS NULL
                                           EXEC(N'CREATE TYPE dbo.CaeriusTestIntList AS TABLE (Value int NOT NULL)');
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_widget_by_id
                                           @id int
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           SELECT Id, Name FROM dbo.CaeriusWidgets WHERE Id = @id;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_count_ints
                                           @values dbo.CaeriusTestIntList READONLY
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           SELECT COUNT_BIG(*) FROM @values;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_noop
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                       END
                                       """, cancellationToken);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string text, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = text;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record Widget(int Id, string Name) : ISpMapper<Widget>
    {
        public static Widget MapFromDataReader(SqlDataReader reader)
        {
            return new Widget(reader.GetInt32(0), reader.GetString(1));
        }
    }

    private readonly record struct IntRow(int Value) : ITvpMapper<IntRow>
    {
        private static readonly SqlMetaData[] SqlMetadata = [new("Value", SqlDbType.Int)];

        public static string SqlTypeName => "dbo.CaeriusTestIntList";
        public static ImmutableArray<SqlMetaData> Metadata => [.. SqlMetadata];

        public static SqlDataRecord CreateRecord()
        {
            return new SqlDataRecord(SqlMetadata);
        }

        public static void WriteRow(SqlDataRecord record, IntRow row)
        {
            record.SetInt32(0, row.Value);
        }
    }

    private sealed class WidgetCodec : ICaeriusCacheCodec<Widget>
    {
        public string CodecId => "caerius.tests.widget";
        public string ContractHash => "test-widget";

        public void Encode(IBufferWriter<byte> writer, Widget value)
        {
            var binary = new CaeriusBinaryWriter(writer);
            binary.WriteInt32(value.Id);
            binary.WriteString(value.Name);
        }

        public Widget Decode(ReadOnlySpan<byte> payload)
        {
            var binary = new CaeriusBinaryReader(payload);
            var value = new Widget(binary.ReadInt32(), binary.ReadString());
            if (binary.Remaining != 0)
                throw new CaeriusContractException("The Widget cache payload contains trailing bytes.");
            return value;
        }
    }

    private sealed class FailingRemoteCache : ICaeriusRemoteCacheManager
    {
        public ValueTask<CaeriusCacheLookup<T>> TryGetAsync<T>(
            string key,
            ICaeriusCacheCodec<T> codec,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<CaeriusCacheLookup<T>>(
                new InvalidOperationException("Redis is unavailable."));
        }

        public ValueTask SetAsync<T>(
            string key,
            T value,
            ICaeriusCacheCodec<T> codec,
            TimeSpan? expiration,
            CancellationToken cancellationToken = default) where T : notnull
        {
            return ValueTask.FromException(new InvalidOperationException("Redis is unavailable."));
        }

        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException(new InvalidOperationException("Redis is unavailable."));
        }
    }
}
