using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using CaeriusNet.Benchmark.Workshops;
using CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections;
using CaeriusNet.Benchmark.Workshops.Benchs.ListCapacity;
using CaeriusNet.Benchmark.Workshops.Benchs.Mapping;
using CaeriusNet.Benchmark.Workshops.Benchs.Parameters;
using CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;
using CaeriusNet.Benchmark.Workshops.Benchs.SqlServer;
using CaeriusNet.Benchmark.Workshops.Benchs.Tvp;

namespace CaeriusNet.Benchmark;

/// <summary>
///     Entry point for all CaeriusNet benchmark suites.
///     Use the category methods to run specific groups or Run_All_Benchmarks() for a full suite.
/// </summary>
public static class RunningBenchmarks
{
    private static IConfig GetConfig() => new BenchmarkConfig();

    /// <summary>Runs ALL benchmarks (in-memory + collection + SQL Server).</summary>
    public static void Run_All_Benchmarks()
    {
        BenchmarkRunner.Run([
            // In-memory: DTO mapping patterns
            typeof(DtoMappingBench),
            // In-memory: StoredProcedureParametersBuilder overhead
            typeof(SpParameterBuilderBench),
            // In-memory: TVP serialization
            typeof(TvpSerializationBench),
            // Collections: read performance
            typeof(ReadListToBench),
            typeof(ReadReadOnlyCollectionToBench),
            typeof(ReadEnumerableToBench),
            typeof(ReadImmutableArrayToBench),
            // Collections: creation performance
            typeof(CreateListToBench),
            typeof(CreateReadOnlyCollectionToBench),
            typeof(CreateEnumerableToBench),
            typeof(CreateImmutableArrayToBench),
            // Collections: capacity pre-allocation
            typeof(ListWithoutCapacityToBench),
            typeof(ListWithCapacityToBench),
            typeof(ListWithCapacityWithOverextendToBench),
            typeof(ListWithLessCapacityThanNeededToBench),
            // SQL Server (skipped automatically if BENCHMARK_SQL_CONNECTION not set)
            typeof(SpExecutionBench),
            typeof(BatchedVsSingleBench),
            typeof(MultiResultSetBench)
        ], GetConfig());
    }

    /// <summary>Runs only the in-memory benchmarks (no SQL Server required).</summary>
    public static void Run_InMemory_Benchmarks()
    {
        BenchmarkRunner.Run([
            typeof(DtoMappingBench),
            typeof(SpParameterBuilderBench),
            typeof(TvpSerializationBench)
        ], GetConfig());
    }

    /// <summary>Runs only the SQL Server benchmarks (requires BENCHMARK_SQL_CONNECTION env var).</summary>
    public static void Run_SqlServer_Benchmarks()
    {
        if (!SqlBenchmarkGlobalSetup.IsSqlAvailable)
        {
            Console.WriteLine("⚠️  BENCHMARK_SQL_CONNECTION is not set — skipping SQL Server benchmarks.");
            return;
        }

        BenchmarkRunner.Run([
            typeof(SpExecutionBench),
            typeof(BatchedVsSingleBench),
            typeof(MultiResultSetBench)
        ], GetConfig());
    }

    /// <summary>Runs all collection-type benchmarks (read, create, capacity).</summary>
    public static void Run_Collections_Benchmarks()
    {
        BenchmarkRunner.Run([
            typeof(ReadListToBench),
            typeof(ReadReadOnlyCollectionToBench),
            typeof(ReadEnumerableToBench),
            typeof(ReadImmutableArrayToBench),
            typeof(CreateListToBench),
            typeof(CreateReadOnlyCollectionToBench),
            typeof(CreateEnumerableToBench),
            typeof(CreateImmutableArrayToBench),
            typeof(ListWithoutCapacityToBench),
            typeof(ListWithCapacityToBench),
            typeof(ListWithCapacityWithOverextendToBench),
            typeof(ListWithLessCapacityThanNeededToBench)
        ], GetConfig());
    }
}