using BenchmarkDotNet.Loggers;

namespace CaeriusNet.Benchmark.Workshops;

/// <summary>
///     BenchmarkDotNet configuration.
///     CI (env CI=true): in-process execution, explicit artifact path, GitHub Markdown + JSON export.
///     Local: default out-of-process execution, HTML + JSON export.
/// </summary>
public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        var isCi = string.Equals(
            Environment.GetEnvironmentVariable("CI"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        // Explicit path avoids non-deterministic CWD issues with child process spawning.
        // BENCHMARK_ARTIFACTS_PATH is set by the CI workflow; falls back to CWD-relative default locally.
        var artifactsPath = Environment.GetEnvironmentVariable("BENCHMARK_ARTIFACTS_PATH")
                            ?? Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkDotNet.Artifacts");
        WithArtifactsPath(artifactsPath);

        if (isCi)
        {
            // InProcessEmitToolchain: runs benchmarks in the host process (no child process spawning).
            // This guarantees artifacts are always written to the configured path above.
            // WarmupCount=1, IterationCount=3 → real measurements, CI-fast.
            AddJob(Job.Default
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithWarmupCount(1)
                .WithIterationCount(5));
        }
        else
        {
            AddJob(Job.Default);
            AddExporter(HtmlExporter.Default);
        }

        AddExporter(MarkdownExporter.GitHub); // → *-report-github.md (GitHub-flavoured markdown table)
        AddExporter(JsonExporter.Full); // → *-report-full.json
        AddLogger(ConsoleLogger.Default);
        AddDiagnoser(MemoryDiagnoser.Default);
        WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }
}
