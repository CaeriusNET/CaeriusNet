using Microsoft.Data.SqlClient;

namespace CaeriusNet.Benchmark.Workshops.Benchs.SqlServer;

/// <summary>
///     Shared SQL Server setup for benchmark classes.
///     Reads connection string from BENCHMARK_SQL_CONNECTION env var.
///     Creates the database schema and stored procedures needed by all SQL benchmarks.
/// </summary>
public static class SqlBenchmarkGlobalSetup
{
    public static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTION");

    public static bool IsSqlAvailable => !string.IsNullOrWhiteSpace(ConnectionString);

    private const string CreateTableSql = """
        IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BenchmarkItems]') AND type = 'U')
        CREATE TABLE [dbo].[BenchmarkItems] (
            [Id]       INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
            [Name]     NVARCHAR(100) NOT NULL,
            [Price]    DECIMAL(18,2) NOT NULL,
            [IsActive] BIT           NOT NULL DEFAULT 1
        );
        """;

    private const string CreateTvpTypeSql = """
        IF NOT EXISTS (SELECT * FROM sys.types WHERE name = 'tvp_BenchmarkItem' AND is_user_defined = 1)
        EXEC('CREATE TYPE [dbo].[tvp_BenchmarkItem] AS TABLE (
            [Id]    INT           NULL,
            [Name]  NVARCHAR(100) NOT NULL,
            [Price] DECIMAL(18,2) NOT NULL
        )');
        """;

    private const string CreateGetItemsSpSql = """
        IF OBJECT_ID('[dbo].[usp_GetBenchmarkItems]', 'P') IS NOT NULL
            DROP PROCEDURE [dbo].[usp_GetBenchmarkItems];
        """;

    private const string CreateGetItemsSpBodySql = """
        CREATE PROCEDURE [dbo].[usp_GetBenchmarkItems]
            @Count INT = 100
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT TOP (@Count) [Id], [Name], [Price], [IsActive]
            FROM [dbo].[BenchmarkItems];
        END
        """;

    private const string CreateInsertItemSpSql = """
        IF OBJECT_ID('[dbo].[usp_InsertBenchmarkItem]', 'P') IS NOT NULL
            DROP PROCEDURE [dbo].[usp_InsertBenchmarkItem];
        """;

    private const string CreateInsertItemSpBodySql = """
        CREATE PROCEDURE [dbo].[usp_InsertBenchmarkItem]
            @Name  NVARCHAR(100),
            @Price DECIMAL(18,2)
        AS
        BEGIN
            SET NOCOUNT ON;
            INSERT INTO [dbo].[BenchmarkItems] ([Name], [Price])
            VALUES (@Name, @Price);
        END
        """;

    private const string CreateInsertBatchSpSql = """
        IF OBJECT_ID('[dbo].[usp_InsertBenchmarkItemsBatch]', 'P') IS NOT NULL
            DROP PROCEDURE [dbo].[usp_InsertBenchmarkItemsBatch];
        """;

    private const string CreateInsertBatchSpBodySql = """
        CREATE PROCEDURE [dbo].[usp_InsertBenchmarkItemsBatch]
            @Items [dbo].[tvp_BenchmarkItem] READONLY
        AS
        BEGIN
            SET NOCOUNT ON;
            INSERT INTO [dbo].[BenchmarkItems] ([Name], [Price])
            SELECT [Name], [Price] FROM @Items;
        END
        """;

    // TVP type with 5 columns — decimal precision matches generator output (DECIMAL(18,4))
    private const string CreateTvpItem5ColTypeSql = """
        IF NOT EXISTS (SELECT * FROM sys.types WHERE name = 'tvp_BenchmarkItem5Col' AND is_user_defined = 1)
        EXEC('CREATE TYPE [dbo].[tvp_BenchmarkItem5Col] AS TABLE (
            [Id]          INT              NULL,
            [Name]        NVARCHAR(100)    NOT NULL,
            [Price]       DECIMAL(18,4)    NOT NULL,
            [IsActive]    BIT              NOT NULL,
            [CreatedDate] DATETIME2        NOT NULL
        )');
        """;

    // SP: TVP batch INSERT returning inserted rows via OUTPUT clause
    private const string CreateInsertBatchWithOutputSpSql = """
        IF OBJECT_ID('[dbo].[usp_InsertBenchmarkItemsBatch_WithOutput]', 'P') IS NOT NULL
            DROP PROCEDURE [dbo].[usp_InsertBenchmarkItemsBatch_WithOutput];
        """;

    private const string CreateInsertBatchWithOutputSpBodySql = """
        CREATE PROCEDURE [dbo].[usp_InsertBenchmarkItemsBatch_WithOutput]
            @Items [dbo].[tvp_BenchmarkItem5Col] READONLY
        AS
        BEGIN
            SET NOCOUNT ON;
            INSERT INTO [dbo].[BenchmarkItems] ([Name], [Price])
            OUTPUT INSERTED.[Id], INSERTED.[Name], INSERTED.[Price]
            SELECT [Name], [Price] FROM @Items;
        END
        """;

    // SP: Single row INSERT with OUTPUT parameter using SCOPE_IDENTITY()
    private const string CreateInsertItemWithOutputSpSql = """
        IF OBJECT_ID('[dbo].[usp_InsertBenchmarkItemWithOutput]', 'P') IS NOT NULL
            DROP PROCEDURE [dbo].[usp_InsertBenchmarkItemWithOutput];
        """;

    private const string CreateInsertItemWithOutputSpBodySql = """
        CREATE PROCEDURE [dbo].[usp_InsertBenchmarkItemWithOutput]
            @Name  NVARCHAR(100),
            @Price DECIMAL(18,2),
            @NewId INT OUTPUT
        AS
        BEGIN
            SET NOCOUNT ON;
            INSERT INTO [dbo].[BenchmarkItems] ([Name], [Price])
            VALUES (@Name, @Price);
            SET @NewId = SCOPE_IDENTITY();
        END
        """;

    private const string SeedDataSql = """
        IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[BenchmarkItems])
        BEGIN
            INSERT INTO [dbo].[BenchmarkItems] ([Name], [Price], [IsActive])
            SELECT TOP 10000
                CONCAT('Item_', ROW_NUMBER() OVER (ORDER BY (SELECT NULL))),
                CAST(ABS(CHECKSUM(NEWID())) % 9999 + 1 AS DECIMAL(18,2)),
                1
            FROM sys.all_objects a CROSS JOIN sys.all_objects b;
        END
        """;

    /// <summary>
    ///     Executes all DDL statements needed by SQL benchmarks.
    ///     Safe to call multiple times (idempotent).
    /// </summary>
    public static async Task InitialiseAsync()
    {
        if (!IsSqlAvailable) return;

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        foreach (var sql in new[]
        {
            CreateTableSql,
            CreateTvpTypeSql,
            CreateTvpItem5ColTypeSql,
            CreateGetItemsSpSql,
            CreateGetItemsSpBodySql,
            CreateInsertItemSpSql,
            CreateInsertItemSpBodySql,
            CreateInsertBatchSpSql,
            CreateInsertBatchSpBodySql,
            CreateInsertBatchWithOutputSpSql,
            CreateInsertBatchWithOutputSpBodySql,
            CreateInsertItemWithOutputSpSql,
            CreateInsertItemWithOutputSpBodySql,
            SeedDataSql
        })
        {
            await using var cmd = new SqlCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
