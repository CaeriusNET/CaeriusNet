using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;

namespace CaeriusNet.Benchmark.Workshops;

/// <summary>
///     BenchmarkDotNet configuration.
///     In CI (env var CI=true): uses Job.Short (reduced warmup/iterations) to avoid timeout.
///     Locally: uses default config with HTML + JSON exporters.
/// </summary>
public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        var isCI = string.Equals(
            Environment.GetEnvironmentVariable("CI"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        AddJob(isCI
            ? Job.Dry.WithWarmupCount(1).WithIterationCount(3)
            : Job.Default);

        AddExporter(JsonExporter.Full);

        if (!isCI)
            AddExporter(HtmlExporter.Default);

        AddDiagnoser(MemoryDiagnoser.Default);

        WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }
}
