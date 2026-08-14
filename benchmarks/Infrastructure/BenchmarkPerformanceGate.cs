namespace CaeriusNet.Benchmark.Infrastructure;

/// <summary>
///     Compares BenchmarkDotNet full JSON reports without relying on Markdown formatting. The gate is deliberately
///     strict: a benchmark that is added, removed, or lacks a valid baseline requires an explicit baseline update.
/// </summary>
internal static class BenchmarkPerformanceGate
{
    private const double DefaultMaximumTimeRegressionPercent = 5d;
    private const string ResultsPattern = "*-report-full*.json";

    public static int Compare(string[] arguments)
    {
        if (!TryReadComparisonArguments(arguments, out var baselineDirectory, out var candidateDirectory,
                out var maximumTimeRegressionPercent, out var error))
        {
            Console.Error.WriteLine(error);
            return 64;
        }

        if (!Directory.Exists(baselineDirectory))
        {
            Console.Error.WriteLine($"Benchmark baseline is required but does not exist: {baselineDirectory}");
            Console.Error.WriteLine(
                "Run the manual workflow with mode=record, review the artifact, then commit it under benchmarks/baselines/12.0.0.");
            return 2;
        }

        try
        {
            var baseline = LoadMeasurements(baselineDirectory);
            var candidate = LoadMeasurements(candidateDirectory);
            var evaluation = Evaluate(baseline, candidate, maximumTimeRegressionPercent);
            var reportPath = Path.Combine(candidateDirectory, "caerius-benchmark-comparison.md");
            File.WriteAllText(reportPath, CreateMarkdownReport(evaluation, baselineDirectory, candidateDirectory,
                maximumTimeRegressionPercent), new UTF8Encoding(false));
            Console.WriteLine($"Benchmark comparison: {reportPath}");
            foreach (var failure in evaluation.Failures)
                Console.Error.WriteLine(failure);
            return evaluation.Failures.Count == 0 ? 0 : 1;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
        {
            Console.Error.WriteLine("Unable to compare benchmark reports: " + exception.Message);
            return 2;
        }
    }

    public static int Record(string[] arguments)
    {
        if (arguments.Length != 2)
        {
            Console.Error.WriteLine("Usage: --record-baseline <candidate-results-dir> <empty-output-dir>");
            return 64;
        }

        var sourceDirectory = Path.GetFullPath(arguments[0]);
        var destinationDirectory = Path.GetFullPath(arguments[1]);
        try
        {
            var reports = GetReportFiles(sourceDirectory);
            if (Directory.Exists(destinationDirectory) &&
                Directory.EnumerateFileSystemEntries(destinationDirectory).Any())
                throw new InvalidOperationException($"Baseline destination must be empty: {destinationDirectory}");

            Directory.CreateDirectory(destinationDirectory);
            foreach (var report in reports)
                File.Copy(report, Path.Combine(destinationDirectory, Path.GetFileName(report)), false);

            _ = LoadMeasurements(destinationDirectory);
            Console.WriteLine($"Recorded {reports.Count} BenchmarkDotNet JSON report(s) into {destinationDirectory}.");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
        {
            Console.Error.WriteLine("Unable to record benchmark baseline: " + exception.Message);
            return 2;
        }
    }

    private static bool TryReadComparisonArguments(
        string[] arguments,
        out string baselineDirectory,
        out string candidateDirectory,
        out double maximumTimeRegressionPercent,
        out string error)
    {
        baselineDirectory = string.Empty;
        candidateDirectory = string.Empty;
        maximumTimeRegressionPercent = DefaultMaximumTimeRegressionPercent;
        error = string.Empty;

        if (arguments.Length is < 2 or > 4)
        {
            error = "Usage: --compare <baseline-results-dir> <candidate-results-dir> [--max-time-regression <percent>]";
            return false;
        }

        baselineDirectory = Path.GetFullPath(arguments[0]);
        candidateDirectory = Path.GetFullPath(arguments[1]);
        if (arguments.Length == 2)
            return true;
        if (arguments.Length != 4 || !string.Equals(arguments[2], "--max-time-regression", StringComparison.Ordinal) ||
            !double.TryParse(arguments[3], CultureInfo.InvariantCulture, out maximumTimeRegressionPercent) ||
            maximumTimeRegressionPercent < 0)
        {
            error = "--max-time-regression must be a non-negative invariant-culture percentage.";
            return false;
        }

        return true;
    }

    private static Dictionary<string, BenchmarkMeasurement> LoadMeasurements(string directory)
    {
        var reports = GetReportFiles(directory);
        var measurements = new Dictionary<string, BenchmarkMeasurement>(StringComparer.Ordinal);
        foreach (var report in reports)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(report));
            if (!document.RootElement.TryGetProperty("Benchmarks", out var benchmarks) ||
                benchmarks.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException($"'{report}' is not a BenchmarkDotNet full JSON report.");

            foreach (var benchmark in benchmarks.EnumerateArray())
            {
                var fullName = ReadRequiredString(benchmark, "FullName", report);
                var parameters = benchmark.TryGetProperty("Parameters", out var parameterElement)
                    ? parameterElement.GetString() ?? string.Empty
                    : string.Empty;
                var statistics = ReadRequiredObject(benchmark, "Statistics", report);
                var medianNanoseconds = ReadRequiredDouble(statistics, "Median", report);
                if (medianNanoseconds <= 0)
                    throw new InvalidOperationException($"'{report}' reports a non-positive median for '{fullName}'.");

                var allocatedBytes = 0L;
                if (benchmark.TryGetProperty("Memory", out var memory) && memory.ValueKind == JsonValueKind.Object &&
                    memory.TryGetProperty("BytesAllocatedPerOperation", out var allocated) &&
                    allocated.ValueKind != JsonValueKind.Null)
                    allocatedBytes = checked((long)Math.Ceiling(allocated.GetDouble()));

                var key = string.IsNullOrEmpty(parameters) ? fullName : fullName + " (" + parameters + ')';
                if (!measurements.TryAdd(key, new BenchmarkMeasurement(key, medianNanoseconds, allocatedBytes)))
                    throw new InvalidOperationException($"Duplicate benchmark identity '{key}' in '{directory}'.");
            }
        }

        if (measurements.Count == 0)
            throw new InvalidOperationException($"No benchmark measurements were found under '{directory}'.");
        return measurements;
    }

    private static List<string> GetReportFiles(string directory)
    {
        if (!Directory.Exists(directory))
            throw new InvalidOperationException($"Benchmark result directory does not exist: {directory}");
        var reports = Directory.EnumerateFiles(directory, ResultsPattern, SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToList();
        if (reports.Count == 0)
            throw new InvalidOperationException($"No '{ResultsPattern}' files were found under '{directory}'.");
        return reports;
    }

    private static Evaluation Evaluate(
        IReadOnlyDictionary<string, BenchmarkMeasurement> baseline,
        IReadOnlyDictionary<string, BenchmarkMeasurement> candidate,
        double maximumTimeRegressionPercent)
    {
        var rows = new List<ComparisonRow>();
        var failures = new List<string>();

        foreach (var (key, baselineMeasurement) in baseline.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!candidate.TryGetValue(key, out var candidateMeasurement))
            {
                failures.Add($"Missing candidate benchmark: {key}");
                continue;
            }

            var timeChangePercent =
                (candidateMeasurement.MedianNanoseconds / baselineMeasurement.MedianNanoseconds - 1d) * 100d;
            var allocationChange = candidateMeasurement.AllocatedBytes - baselineMeasurement.AllocatedBytes;
            var timeRegressed = timeChangePercent > maximumTimeRegressionPercent;
            var allocationRegressed = allocationChange > 0;
            rows.Add(new ComparisonRow(key, baselineMeasurement, candidateMeasurement, timeChangePercent,
                allocationChange,
                timeRegressed || allocationRegressed));

            if (timeRegressed)
                failures.Add($"Time regression {timeChangePercent:F2}% (> {maximumTimeRegressionPercent:F2}%): {key}");
            if (allocationRegressed)
                failures.Add($"Allocation regression +{allocationChange} B/op: {key}");
        }

        foreach (var key in candidate.Keys.Except(baseline.Keys, StringComparer.Ordinal)
                     .OrderBy(static key => key, StringComparer.Ordinal))
            failures.Add($"New benchmark has no baseline: {key}");

        return new Evaluation(rows, failures);
    }

    private static string CreateMarkdownReport(
        Evaluation evaluation,
        string baselineDirectory,
        string candidateDirectory,
        double maximumTimeRegressionPercent)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# CaeriusNet 12.0 benchmark comparison");
        builder.AppendLine();
        builder.Append("- Baseline: `").Append(baselineDirectory).AppendLine("`");
        builder.Append("- Candidate: `").Append(candidateDirectory).AppendLine("`");
        builder.Append("- Time threshold: ")
            .Append(maximumTimeRegressionPercent.ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine("%");
        builder.Append("- Gate: ").AppendLine(evaluation.Failures.Count == 0 ? "passed" : "failed");
        builder.AppendLine();
        builder.AppendLine("| Benchmark | Baseline median | Candidate median | Δ time | Δ allocated | Status |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | --- |");
        foreach (var row in evaluation.Rows)
            builder.Append("| `").Append(row.Key).Append("` | ")
                .Append(FormatNanoseconds(row.Baseline.MedianNanoseconds)).Append(" | ")
                .Append(FormatNanoseconds(row.Candidate.MedianNanoseconds)).Append(" | ")
                .Append(row.TimeChangePercent.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)).Append("% | ")
                .Append(row.AllocationChange.ToString("+0;-0;0", CultureInfo.InvariantCulture)).Append(" B/op | ")
                .AppendLine(row.Failed ? "fail" : "pass");

        if (evaluation.Failures.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Failures");
            foreach (var failure in evaluation.Failures)
                builder.Append("- ").AppendLine(failure);
        }

        return builder.ToString();
    }

    private static string FormatNanoseconds(double nanoseconds)
    {
        return nanoseconds >= 1_000_000d
            ? (nanoseconds / 1_000_000d).ToString("F3", CultureInfo.InvariantCulture) + " ms"
            : nanoseconds >= 1_000d
                ? (nanoseconds / 1_000d).ToString("F3", CultureInfo.InvariantCulture) + " µs"
                : nanoseconds.ToString("F1", CultureInfo.InvariantCulture) + " ns";
    }

    private static JsonElement ReadRequiredObject(JsonElement parent, string propertyName, string file)
    {
        return parent.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object
            ? property
            : throw new InvalidOperationException($"'{file}' is missing object property '{propertyName}'.");
    }

    private static string ReadRequiredString(JsonElement parent, string propertyName, string file)
    {
        return parent.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(property.GetString())
            ? property.GetString()!
            : throw new InvalidOperationException($"'{file}' is missing string property '{propertyName}'.");
    }

    private static double ReadRequiredDouble(JsonElement parent, string propertyName, string file)
    {
        return parent.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number &&
               property.TryGetDouble(out var value)
            ? value
            : throw new InvalidOperationException($"'{file}' is missing numeric property '{propertyName}'.");
    }

    private sealed record BenchmarkMeasurement(string Key, double MedianNanoseconds, long AllocatedBytes);

    private sealed record ComparisonRow(
        string Key,
        BenchmarkMeasurement Baseline,
        BenchmarkMeasurement Candidate,
        double TimeChangePercent,
        long AllocationChange,
        bool Failed);

    private sealed record Evaluation(IReadOnlyList<ComparisonRow> Rows, IReadOnlyList<string> Failures);
}
