using Xunit.Sdk;

namespace CaeriusNet.IntegrationTests.Infrastructure;

public static class ContainerTestGate
{
    private const string RunContainersVariable = "CAERIUS_RUN_CONTAINERS";
    private const string SqlImageVariable = "CAERIUS_TEST_SQL_IMAGE";

    public static IEnumerable<object[]> SqlServerImages
    {
        get
        {
            var configuredImage = Environment.GetEnvironmentVariable(SqlImageVariable);
            if (!string.IsNullOrWhiteSpace(configuredImage))
            {
                yield return [configuredImage];
                yield break;
            }

            yield return ["mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04"];
            yield return ["mcr.microsoft.com/mssql/server:2025-CU7-ubuntu-24.04"];
        }
    }

    internal static void RequireEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunContainersVariable), "true",
                StringComparison.OrdinalIgnoreCase))
            throw SkipException.ForSkip(
                $"Set {RunContainersVariable}=true to run Testcontainers acceptance tests.");
    }
}
