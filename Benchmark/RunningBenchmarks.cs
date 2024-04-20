using BenchmarkDotNet.Configs;
using CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections;
using CaeriusNet.Benchmark.Workshops.Benchs.ListCapacity;
using CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

namespace CaeriusNet.Benchmark;

public static class RunningBenchmarks
{
    public static void Run_All_Benchmarks()
    {
        BenchmarkRunner.Run(new[]
        {
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
        }, ManualConfig.Create(DefaultConfig.Instance));
    }

    public static void Running_All_Read_Collections_Benchmarks()
    {
        BenchmarkRunner.Run(new[]
        {
            typeof(ReadListToBench),
            typeof(ReadReadOnlyCollectionToBench),
            typeof(ReadEnumerableToBench),
            typeof(ReadImmutableArrayToBench)
        }, ManualConfig.Create(DefaultConfig.Instance));
    }

    public static void Running_All_Create_Collections_Benchmarks()
    {
        BenchmarkRunner.Run(new[]
        {
            typeof(CreateListToBench),
            typeof(CreateReadOnlyCollectionToBench),
            typeof(CreateEnumerableToBench),
            typeof(CreateImmutableArrayToBench)
        }, ManualConfig.Create(DefaultConfig.Instance));
    }

    public static void Running_All_Add_Collections_Benchmarks()
    {
        BenchmarkRunner.Run(new[]
        {
            typeof(ListWithoutCapacityToBench),
            typeof(ListWithCapacityToBench),
            typeof(ListWithCapacityWithOverextendToBench),
            typeof(ListWithLessCapacityThanNeededToBench)
        }, ManualConfig.Create(DefaultConfig.Instance));
    }
}