namespace CaeriusNet.Benchmark.Infrastructure;

/// <summary>
///     Environment gates for benchmarks that require a real server. Values are intentionally never logged or
///     exported: a connection string can contain credentials even when the benchmark database is disposable.
/// </summary>
internal static class BenchmarkEnvironment
{
    internal const string SqlServerConnectionVariable = "CAERIUS_BENCHMARK_SQL_CONNECTION";
    internal const string RedisConnectionVariable = "CAERIUS_BENCHMARK_REDIS_CONNECTION";

    internal static string RequireSqlServerConnectionString()
    {
        return RequireConnectionString(SqlServerConnectionVariable, "SQL Server");
    }

    internal static string RequireRedisConnectionString()
    {
        return RequireConnectionString(RedisConnectionVariable, "Redis");
    }

    private static string RequireConnectionString(string variableName, string serviceName)
    {
        var connectionString = Environment.GetEnvironmentVariable(variableName);
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        throw new InvalidOperationException(
            $"The {serviceName} benchmark is opt-in. Set {variableName} to a disposable benchmark database/instance before running this category.");
    }
}
