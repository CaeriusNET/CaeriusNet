namespace CaeriusNet.Benchmark.Infrastructure;

/// <summary>
///     Shared configuration for local and CI benchmarks. It always exports the full JSON data used by the
///     performance gate; CI uses more samples than a local exploratory run and never uses BenchmarkDotNet's Dry job.
/// </summary>
internal sealed class BenchmarkConfiguration : ManualConfig
{
    public BenchmarkConfiguration()
    {
        var isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
        var artifactsPath = Environment.GetEnvironmentVariable("BENCHMARK_ARTIFACTS_PATH");
        WithArtifactsPath(string.IsNullOrWhiteSpace(artifactsPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkDotNet.Artifacts")
            : Path.GetFullPath(artifactsPath));

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
