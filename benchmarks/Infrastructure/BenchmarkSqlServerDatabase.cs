namespace CaeriusNet.Benchmark.Infrastructure;

/// <summary>
///     Provisions the deterministic, read-only stored procedures used only by the opt-in SQL Server benchmarks.
///     The procedures deliberately use no application table, so their reader/mapping cost stays comparable between
///     benchmark runs while the benchmark still executes the public SQL Server API end to end.
/// </summary>
internal static class BenchmarkSqlServerDatabase
{
    private const string RowProjection = """
                                         SELECT TOP (32)
                                             CONVERT(int, ROW_NUMBER() OVER (ORDER BY (SELECT NULL))) AS Id,
                                             CONCAT(N'row-', CONVERT(nvarchar(12), ROW_NUMBER() OVER (ORDER BY (SELECT NULL)))) AS Name,
                                             CONVERT(decimal(19,4), 42.5000) AS Amount,
                                             CONVERT(datetimeoffset(7), SYSDATETIMEOFFSET()) AS ChangedAt,
                                             CONVERT(bit, 1) AS Enabled,
                                             CONVERT(nvarchar(32), NULL) AS OptionalName
                                         FROM sys.all_objects;
                                         """;

    internal static async Task ProvisionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (var resultSetCount in new[] { 1, 2, 5, 10 })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = CreateProcedureSql($"caerius_benchmark_reader_{resultSetCount}", resultSetCount);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var redisCommand = connection.CreateCommand();
        redisCommand.CommandText = CreateProcedureSql("caerius_benchmark_redis_reader", 1);
        await redisCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string CreateProcedureSql(string procedureName, int resultSetCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
        ArgumentOutOfRangeException.ThrowIfLessThan(resultSetCount, 1);

        var body = new StringBuilder(resultSetCount * (RowProjection.Length + Environment.NewLine.Length));
        for (var index = 0; index < resultSetCount; index++)
            body.AppendLine(RowProjection);

        // procedureName is supplied exclusively from the fixed constants above, never from configuration.
        return $"CREATE OR ALTER PROCEDURE dbo.{procedureName} AS BEGIN SET NOCOUNT ON; {body} END";
    }
}
