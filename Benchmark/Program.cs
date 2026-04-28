using CaeriusNet.Benchmark;

// In CI: runs all benchmarks (category controlled by CI workflow args)
// Locally: same — use RunningBenchmarks.Run_InMemory_Benchmarks() etc. directly
var category = args.Length > 0 ? args[0].ToLowerInvariant() : "all";

switch (category)
{
    case "in-memory":
        RunningBenchmarks.Run_InMemory_Benchmarks();
        break;
    case "tvp":
        RunningBenchmarks.Run_Tvp_Benchmarks();
        break;
    case "sql":
    case "sql-server":
        RunningBenchmarks.Run_SqlServer_Benchmarks();
        break;
    case "collections":
        RunningBenchmarks.Run_Collections_Benchmarks();
        break;
    case "cache":
        RunningBenchmarks.Run_Cache_Benchmarks();
        break;
    default:
        RunningBenchmarks.Run_All_Benchmarks();
        break;
}
