using CaeriusNet.Benchmark.Caching;
using CaeriusNet.Benchmark.CommandConstruction;
using CaeriusNet.Benchmark.ResultMaterialization;
using CaeriusNet.Benchmark.TableValuedParameters;

namespace CaeriusNet.Benchmark.Infrastructure;

internal static class BenchmarkCommandLine
{
    private static readonly Type[] AllBenchmarks =
    [
        typeof(CommandBuilderBenchmarks),
        typeof(TvpBenchmarks),
        typeof(MaterializationBenchmarks),
        typeof(MultiResultSetMetadataBenchmarks),
        typeof(CacheCodecBenchmarks),
        typeof(CachedAsyncMemoryHitBenchmarks)
    ];

    public static int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length > 0 && string.Equals(args[0], "--compare", StringComparison.Ordinal))
            return BenchmarkPerformanceGate.Compare(args[1..]);
        if (args.Length > 0 && string.Equals(args[0], "--record-baseline", StringComparison.Ordinal))
            return BenchmarkPerformanceGate.Record(args[1..]);
        if (args.Length > 0 && (string.Equals(args[0], "--help", StringComparison.Ordinal) ||
                                string.Equals(args[0], "-h", StringComparison.Ordinal)))
        {
            PrintUsage();
            return 0;
        }

        var category = "all";
        var benchmarkDotNetArguments = args;
        if (args.Length > 0 && args[0][0] != '-')
        {
            category = args[0];
            benchmarkDotNetArguments = args[1..];
        }

        if (!TryResolveCategory(category, out var benchmarkTypes))
        {
            Console.Error.WriteLine($"Unknown benchmark category '{category}'.");
            PrintUsage();
            return 64;
        }

        var summaries = BenchmarkRunner.Run(benchmarkTypes, new BenchmarkConfiguration(), benchmarkDotNetArguments);
        var reports = summaries.SelectMany(static summary => summary.Reports).ToArray();
        if (reports.Length == 0 || summaries.Any(static summary => summary.HasCriticalValidationErrors) ||
            reports.Any(static report => !report.Success || report.ResultStatistics is null))
        {
            Console.Error.WriteLine(
                "BenchmarkDotNet did not produce a complete successful measurement set; no baseline or comparison may use these results.");
            return 1;
        }

        return 0;
    }

    private static bool TryResolveCategory(string category, out Type[] benchmarkTypes)
    {
        benchmarkTypes = category.ToLowerInvariant() switch
        {
            "all" => AllBenchmarks,
            "command" or "parameters" => [typeof(CommandBuilderBenchmarks)],
            "tvp" => [typeof(TvpBenchmarks)],
            "materialization" or "mapping" or "scalar" or "collections" => [typeof(MaterializationBenchmarks)],
            "multi-result" or "multi-results" => [typeof(MultiResultSetMetadataBenchmarks)],
            "cache" or "codec" => [typeof(CacheCodecBenchmarks), typeof(CachedAsyncMemoryHitBenchmarks)],
            _ => []
        };
        return benchmarkTypes.Length != 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("CaeriusNet 12.0 benchmarks");
        Console.WriteLine(
            "  dotnet run -c Release -- [all|command|tvp|materialization|multi-result|cache] [BenchmarkDotNet options]");
        Console.WriteLine(
            "  dotnet run -c Release -- --compare <baseline-results-dir> <candidate-results-dir> [--max-time-regression <percent>]");
        Console.WriteLine("  dotnet run -c Release -- --record-baseline <candidate-results-dir> <empty-output-dir>");
    }
}
