namespace CaeriusNet.Benchmark.Infrastructure;

/// <summary>
///     Shared configuration for local and CI benchmarks. It always exports the full JSON data used by the
///     performance gate; CI uses more samples than a local exploratory run and never uses BenchmarkDotNet's Dry job.
/// </summary>
internal sealed class BenchmarkConfiguration : ManualConfig
{
    public BenchmarkConfiguration(bool includeConfiguredJob)
    {
        var isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
        var artifactsPath = Environment.GetEnvironmentVariable("BENCHMARK_ARTIFACTS_PATH");
        WithArtifactsPath(string.IsNullOrWhiteSpace(artifactsPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkDotNet.Artifacts")
            : Path.GetFullPath(artifactsPath));

        // BenchmarkDotNet adds a CLI job to jobs declared by the configuration rather than replacing them.
        // In particular, `--job Dry` used to execute the normal local job as well. Leave job selection entirely to
        // BenchmarkDotNet when callers explicitly request a job/runtime; otherwise keep the reproducible v12 job.
        if (includeConfiguredJob)
            AddJob(Job.Default
                .WithId(isCi ? "ci" : "local")
                .WithWarmupCount(isCi ? 5 : 3)
                .WithIterationCount(isCi ? 15 : 10));

        AddExporter(MarkdownExporter.GitHub);
        AddExporter(JsonExporter.Full);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddLogger(ConsoleLogger.Default);
        AddDiagnoser(MemoryDiagnoser.Default);
    }
}
