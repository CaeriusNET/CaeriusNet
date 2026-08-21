using System.Security.Cryptography;

namespace CaeriusNet.Benchmark.Infrastructure;

/// <summary>
///     Compares BenchmarkDotNet full JSON reports without relying on Markdown formatting. A comparison is valid only
///     when both report sets carry a verified, compatible environment manifest produced immediately after a successful
///     benchmark run.
/// </summary>
internal static class BenchmarkPerformanceGate
{
    private const string BenchmarkLine = "11.2.0";
    private const int EnvironmentManifestSchemaVersion = 1;
    private const double DefaultMaximumTimeRegressionPercent = 5d;
    private const string ResultsPattern = "*-report-full*.json";
    private const string EnvironmentManifestFileName = "caerius-benchmark-environment.v1.json";

    private static readonly JsonSerializerOptions EnvironmentManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

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
                "Run the manual workflow with mode=record, review the measured artifact and its environment manifest, then commit it under benchmarks/baselines/11.2.0.");
            return 2;
        }

        try
        {
            var baseline = LoadBundle(baselineDirectory);
            var candidate = LoadBundle(candidateDirectory);
            var reportPath = Path.Combine(candidateDirectory, "caerius-benchmark-comparison.md");
            var environmentDifferences = GetEnvironmentDifferences(baseline.Manifest, candidate.Manifest);
            if (environmentDifferences.Count > 0)
            {
                File.WriteAllText(reportPath, CreateIncomparableEnvironmentReport(baselineDirectory, candidateDirectory,
                    baseline.Manifest, candidate.Manifest, environmentDifferences), new UTF8Encoding(false));
                Console.Error.WriteLine(
                    "Benchmark environments are not comparable; no timing or allocation comparison was performed.");
                foreach (var difference in environmentDifferences)
                    Console.Error.WriteLine(difference);
                return 2;
            }

            var evaluation = Evaluate(baseline.Measurements, candidate.Measurements, maximumTimeRegressionPercent);
            File.WriteAllText(reportPath, CreateMarkdownReport(evaluation, baselineDirectory, candidateDirectory,
                maximumTimeRegressionPercent, baseline.Manifest), new UTF8Encoding(false));
            Console.WriteLine($"Benchmark comparison: {reportPath}");
            foreach (var failure in evaluation.Failures)
                Console.Error.WriteLine(failure);
            return evaluation.Failures.Count == 0 ? 0 : 1;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or
                                              UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Unable to compare benchmark reports: " + exception.Message);
            return 2;
        }
    }

    /// <summary>
    ///     Writes the evidence that ties a complete BenchmarkDotNet export to the machine and job which produced it.
    ///     This command intentionally runs after BenchmarkDotNet has returned success; it does not manufacture data.
    /// </summary>
    public static int WriteEnvironmentManifest(string[] arguments)
    {
        if (arguments.Length != 2)
        {
            Console.Error.WriteLine("Usage: --write-environment <candidate-results-dir> <suite-category>");
            return 64;
        }

        var resultsDirectory = Path.GetFullPath(arguments[0]);
        var suite = arguments[1].Trim().ToLowerInvariant();
        if (!IsKnownSuite(suite))
        {
            Console.Error.WriteLine($"Unknown benchmark suite category '{arguments[1]}'.");
            return 64;
        }

        try
        {
            var measurements = LoadMeasurements(resultsDirectory);
            var reportSnapshots = CreateReportSnapshots(resultsDirectory);
            if (reportSnapshots.Sum(static snapshot => snapshot.MeasurementCount) != measurements.Count)
                throw new InvalidOperationException("Benchmark reports contain duplicate benchmark identities.");

            var settings = CaptureSettings(resultsDirectory);
            var environment = CaptureEnvironment(resultsDirectory);
            var manifest = new BenchmarkEnvironmentManifest(
                EnvironmentManifestSchemaVersion,
                BenchmarkLine,
                suite,
                ComputeCompatibilityFingerprint(environment, settings),
                settings,
                environment,
                reportSnapshots);
            var manifestPath = Path.Combine(resultsDirectory, EnvironmentManifestFileName);
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, EnvironmentManifestJsonOptions),
                new UTF8Encoding(false));

            // Re-read the written file and all reports so a partially written or hand-created manifest cannot become a
            // baseline candidate in the next command.
            _ = LoadBundle(resultsDirectory, false);
            Console.WriteLine($"Wrote benchmark environment manifest: {manifestPath}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or
                                              UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Unable to write benchmark environment manifest: " + exception.Message);
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
            var source = LoadBundle(sourceDirectory);
            if (Directory.Exists(destinationDirectory) &&
                Directory.EnumerateFileSystemEntries(destinationDirectory).Any())
                throw new InvalidOperationException($"Baseline destination must be empty: {destinationDirectory}");

            Directory.CreateDirectory(destinationDirectory);
            foreach (var report in source.Manifest.Reports)
            {
                var sourceReport = GetSafeReportPath(sourceDirectory, report.RelativePath);
                var destinationReport = GetSafeReportPath(destinationDirectory, report.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationReport)!);
                File.Copy(sourceReport, destinationReport, false);
            }

            File.Copy(Path.Combine(sourceDirectory, EnvironmentManifestFileName),
                Path.Combine(destinationDirectory, EnvironmentManifestFileName), false);
            _ = LoadBundle(destinationDirectory);
            Console.WriteLine(
                $"Recorded {source.Manifest.Reports.Count} measured BenchmarkDotNet JSON report(s) and the environment manifest into {destinationDirectory}.");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or
                                              UnauthorizedAccessException)
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

    private static BenchmarkBundle LoadBundle(string directory, bool requireCompleteSuite = true)
    {
        var measurements = LoadMeasurements(directory);
        var manifest = LoadEnvironmentManifest(directory);
        ValidateEnvironmentManifest(directory, manifest, measurements, requireCompleteSuite);
        return new BenchmarkBundle(measurements, manifest);
    }

    private static BenchmarkEnvironmentManifest LoadEnvironmentManifest(string directory)
    {
        var manifestPath = Path.Combine(directory, EnvironmentManifestFileName);
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException(
                $"'{EnvironmentManifestFileName}' is required beside the BenchmarkDotNet reports. Run --write-environment immediately after a successful benchmark run.");

        var manifest = JsonSerializer.Deserialize<BenchmarkEnvironmentManifest>(File.ReadAllText(manifestPath),
            EnvironmentManifestJsonOptions);
        return manifest ?? throw new InvalidOperationException($"'{manifestPath}' is empty or invalid.");
    }

    private static void ValidateEnvironmentManifest(
        string directory,
        BenchmarkEnvironmentManifest manifest,
        IReadOnlyDictionary<string, BenchmarkMeasurement> measurements,
        bool requireCompleteSuite)
    {
        if (manifest.SchemaVersion != EnvironmentManifestSchemaVersion)
            throw new InvalidOperationException(
                $"Unsupported benchmark environment manifest schema {manifest.SchemaVersion}; expected {EnvironmentManifestSchemaVersion}.");
        if (!string.Equals(manifest.BenchmarkLine, BenchmarkLine, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Benchmark environment manifest targets line '{manifest.BenchmarkLine}', but this gate accepts '{BenchmarkLine}'.");
        if (!IsKnownSuite(manifest.Suite))
            throw new InvalidOperationException(
                $"Benchmark environment manifest has an unknown suite '{manifest.Suite}'.");
        if (requireCompleteSuite && !string.Equals(manifest.Suite, "all", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Benchmark comparison requires suite 'all'; '{directory}' was recorded as '{manifest.Suite}'.");
        if (manifest.Environment is null || manifest.Settings is null || manifest.Reports is null ||
            manifest.Reports.Count == 0 || string.IsNullOrWhiteSpace(manifest.CompatibilityFingerprint))
            throw new InvalidOperationException(
                "Benchmark environment manifest is missing required environment, settings, or report evidence.");
        if (manifest.Settings.JobSignatures is null || manifest.Settings.JobSignatures.Count == 0 ||
            manifest.Settings.JobSignatures.Any(static signature => string.IsNullOrWhiteSpace(signature)) ||
            !manifest.Settings.MemoryDiagnoserEnabled)
            throw new InvalidOperationException(
                "Benchmark environment manifest must identify every BenchmarkDotNet job and enable MemoryDiagnoser.");

        var expectedFingerprint = ComputeCompatibilityFingerprint(manifest.Environment, manifest.Settings);
        if (!string.Equals(manifest.CompatibilityFingerprint, expectedFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Benchmark environment manifest compatibility fingerprint does not match its declared environment. Regenerate it from measured reports.");

        var actualReports = CreateReportSnapshots(directory);
        var expectedByPath = new Dictionary<string, BenchmarkReportSnapshot>(StringComparer.Ordinal);
        foreach (var expected in manifest.Reports)
        {
            if (string.IsNullOrWhiteSpace(expected.RelativePath) || expected.MeasurementCount <= 0 ||
                string.IsNullOrWhiteSpace(expected.Sha256))
                throw new InvalidOperationException(
                    "Benchmark environment manifest contains an incomplete report entry.");
            _ = GetSafeReportPath(directory, expected.RelativePath);
            if (!expectedByPath.TryAdd(expected.RelativePath, expected))
                throw new InvalidOperationException(
                    $"Benchmark environment manifest has duplicate report entry '{expected.RelativePath}'.");
        }

        if (actualReports.Count != expectedByPath.Count)
            throw new InvalidOperationException(
                "Benchmark environment manifest report set does not match the BenchmarkDotNet reports on disk.");
        foreach (var actual in actualReports)
            if (!expectedByPath.TryGetValue(actual.RelativePath, out var expected) ||
                !string.Equals(actual.Sha256, expected.Sha256, StringComparison.Ordinal) ||
                actual.MeasurementCount != expected.MeasurementCount)
                throw new InvalidOperationException(
                    $"Benchmark report '{actual.RelativePath}' does not match the measured report evidence in the environment manifest.");

        if (actualReports.Sum(static snapshot => snapshot.MeasurementCount) != measurements.Count)
            throw new InvalidOperationException("Benchmark reports contain duplicate benchmark identities.");
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

                if (!benchmark.TryGetProperty("Memory", out var memory) || memory.ValueKind != JsonValueKind.Object ||
                    !memory.TryGetProperty("BytesAllocatedPerOperation", out var allocated) ||
                    allocated.ValueKind != JsonValueKind.Number || !allocated.TryGetDouble(out var allocatedDouble) ||
                    allocatedDouble < 0)
                    throw new InvalidOperationException(
                        $"'{report}' is missing a non-negative Memory.BytesAllocatedPerOperation value for '{fullName}'.");
                var allocatedBytes = checked((long)Math.Ceiling(allocatedDouble));

                var key = string.IsNullOrEmpty(parameters) ? fullName : fullName + " (" + parameters + ')';
                if (!measurements.TryAdd(key, new BenchmarkMeasurement(key, medianNanoseconds, allocatedBytes)))
                    throw new InvalidOperationException($"Duplicate benchmark identity '{key}' in '{directory}'.");
            }
        }

        if (measurements.Count == 0)
            throw new InvalidOperationException($"No benchmark measurements were found under '{directory}'.");
        return measurements;
    }

    private static List<BenchmarkReportSnapshot> CreateReportSnapshots(string directory)
    {
        var rootDirectory = Path.GetFullPath(directory);
        return GetReportFiles(rootDirectory)
            .Select(report => new BenchmarkReportSnapshot(
                Path.GetRelativePath(rootDirectory, report).Replace(Path.DirectorySeparatorChar, '/'),
                ComputeSha256(File.ReadAllBytes(report)),
                ReadMeasurementCount(report)))
            .OrderBy(static snapshot => snapshot.RelativePath, StringComparer.Ordinal)
            .ToList();
    }

    private static int ReadMeasurementCount(string report)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(report));
        if (!document.RootElement.TryGetProperty("Benchmarks", out var benchmarks) ||
            benchmarks.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"'{report}' is not a BenchmarkDotNet full JSON report.");

        var count = 0;
        foreach (var benchmark in benchmarks.EnumerateArray())
        {
            _ = ReadRequiredString(benchmark, "FullName", report);
            var statistics = ReadRequiredObject(benchmark, "Statistics", report);
            var medianNanoseconds = ReadRequiredDouble(statistics, "Median", report);
            if (medianNanoseconds <= 0)
                throw new InvalidOperationException($"'{report}' reports a non-positive median.");
            count++;
        }

        if (count == 0)
            throw new InvalidOperationException($"'{report}' contains no benchmark measurements.");
        return count;
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

    private static string GetSafeReportPath(string rootDirectory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            throw new InvalidOperationException($"Benchmark report path must be relative: '{relativePath}'.");

        var root = Path.GetFullPath(rootDirectory);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Benchmark report path escapes its results directory: '{relativePath}'.");
        return fullPath;
    }

    private static BenchmarkEnvironment CaptureEnvironment(string directory)
    {
        BenchmarkDotNetHostEnvironment? expected = null;
        foreach (var report in GetReportFiles(directory))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(report));
            var host = ReadRequiredObject(document.RootElement, "HostEnvironmentInfo", report);
            var observed = new BenchmarkDotNetHostEnvironment(
                ReadRequiredString(host, "BenchmarkDotNetVersion", report),
                ReadRequiredString(host, "OsVersion", report),
                ReadRequiredString(host, "ProcessorName", report),
                ReadRequiredInt(host, "LogicalCoreCount", report),
                ReadRequiredString(host, "RuntimeVersion", report),
                ReadRequiredString(host, "Architecture", report),
                ReadRequiredString(host, "DotNetCliVersion", report));
            if (expected is null)
                expected = observed;
            else if (expected != observed)
                throw new InvalidOperationException(
                    $"'{report}' was measured in a different BenchmarkDotNet host environment from the other reports.");
        }

        if (expected is null)
            throw new InvalidOperationException(
                "Benchmark reports have no BenchmarkDotNet host environment information.");

        var selectedSdk = Environment.GetEnvironmentVariable("CAERIUS_BENCHMARK_SDK_VERSION");
        if (!string.IsNullOrWhiteSpace(selectedSdk) &&
            !string.Equals(selectedSdk, expected.DotNetCliVersion, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"BenchmarkDotNet reports SDK '{expected.DotNetCliVersion}', but the workflow selected '{selectedSdk}'.");

        return new BenchmarkEnvironment(
            "BenchmarkDotNet target runtime",
            expected.RuntimeVersion,
            expected.OperatingSystemVersion,
            expected.OperatingSystemVersion,
            expected.Architecture,
            expected.Architecture,
            expected.LogicalCoreCount,
            expected.ProcessorName,
            expected.BenchmarkDotNetVersion,
            expected.DotNetCliVersion,
            Environment.GetEnvironmentVariable("RUNNER_OS") ?? "local",
            Environment.GetEnvironmentVariable("RUNNER_ARCH") ?? expected.Architecture,
            Environment.GetEnvironmentVariable("ImageOS") ?? "local",
            Environment.GetEnvironmentVariable("ImageVersion") ?? "unspecified");
    }

    private static BenchmarkSettings CaptureSettings(string directory)
    {
        var jobs = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var report in GetReportFiles(directory))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(report));
            if (!document.RootElement.TryGetProperty("Benchmarks", out var benchmarks) ||
                benchmarks.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException($"'{report}' is not a BenchmarkDotNet full JSON report.");

            foreach (var benchmark in benchmarks.EnumerateArray())
            {
                var displayInfo = ReadRequiredString(benchmark, "DisplayInfo", report);
                var separator = displayInfo.LastIndexOf(": ", StringComparison.Ordinal);
                if (separator < 0 || separator == displayInfo.Length - 2)
                    throw new InvalidOperationException(
                        $"'{report}' has no BenchmarkDotNet job identity in DisplayInfo.");
                jobs.Add(displayInfo[(separator + 2)..]);
            }
        }

        if (jobs.Count == 0)
            throw new InvalidOperationException("Benchmark reports have no job identities.");
        return new BenchmarkSettings(jobs.ToArray(), true);
    }

    private static string ComputeCompatibilityFingerprint(BenchmarkEnvironment environment, BenchmarkSettings settings)
    {
        var values = new[]
        {
            BenchmarkLine,
            environment.FrameworkDescription,
            environment.RuntimeVersion,
            environment.OperatingSystemDescription,
            environment.OperatingSystemVersion,
            environment.OperatingSystemArchitecture,
            environment.ProcessArchitecture,
            environment.ProcessorCount.ToString(CultureInfo.InvariantCulture),
            environment.ProcessorModel,
            environment.BenchmarkDotNetVersion,
            environment.DotNetSdkVersion,
            environment.RunnerOperatingSystem,
            environment.RunnerArchitecture,
            environment.RunnerImage,
            string.Join("\u001f", settings.JobSignatures),
            settings.MemoryDiagnoserEnabled.ToString(CultureInfo.InvariantCulture)
        };
        return ComputeSha256(Encoding.UTF8.GetBytes(string.Join('\n', values)));
    }

    private static string ComputeSha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static List<string> GetEnvironmentDifferences(
        BenchmarkEnvironmentManifest baseline,
        BenchmarkEnvironmentManifest candidate)
    {
        var differences = new List<string>();
        if (!string.Equals(baseline.CompatibilityFingerprint, candidate.CompatibilityFingerprint,
                StringComparison.Ordinal))
        {
            AddDifference(differences, "BenchmarkDotNet", baseline.Environment.BenchmarkDotNetVersion,
                candidate.Environment.BenchmarkDotNetVersion);
            AddDifference(differences, "SDK", baseline.Environment.DotNetSdkVersion,
                candidate.Environment.DotNetSdkVersion);
            AddDifference(differences, "runtime",
                baseline.Environment.FrameworkDescription + " " + baseline.Environment.RuntimeVersion,
                candidate.Environment.FrameworkDescription + " " + candidate.Environment.RuntimeVersion);
            AddDifference(differences, "operating system",
                baseline.Environment.OperatingSystemDescription + " " + baseline.Environment.OperatingSystemVersion,
                candidate.Environment.OperatingSystemDescription + " " + candidate.Environment.OperatingSystemVersion);
            AddDifference(differences, "architectures",
                baseline.Environment.OperatingSystemArchitecture + "/" + baseline.Environment.ProcessArchitecture,
                candidate.Environment.OperatingSystemArchitecture + "/" + candidate.Environment.ProcessArchitecture);
            AddDifference(differences, "processor",
                baseline.Environment.ProcessorModel + " (" + baseline.Environment.ProcessorCount + ")",
                candidate.Environment.ProcessorModel + " (" + candidate.Environment.ProcessorCount + ")");
            AddDifference(differences, "runner", baseline.Environment.RunnerOperatingSystem + "/" +
                                                 baseline.Environment.RunnerArchitecture + "/" +
                                                 baseline.Environment.RunnerImage,
                candidate.Environment.RunnerOperatingSystem + "/" + candidate.Environment.RunnerArchitecture + "/" +
                candidate.Environment.RunnerImage);
            AddDifference(differences, "job", baseline.Settings.ToDisplayString(),
                candidate.Settings.ToDisplayString());
            if (differences.Count == 0)
                differences.Add("The environment compatibility fingerprints differ although no known field differed.");
        }

        return differences;
    }

    private static void AddDifference(List<string> differences, string name, string baseline, string candidate)
    {
        if (!string.Equals(baseline, candidate, StringComparison.Ordinal))
            differences.Add($"Environment mismatch for {name}: baseline '{baseline}', candidate '{candidate}'.");
    }

    private static bool IsKnownSuite(string suite)
    {
        return suite is "all" or "command" or "tvp" or "materialization" or "multi-result" or "cache" or
            "sql-server" or "redis" or "generator";
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
        double maximumTimeRegressionPercent,
        BenchmarkEnvironmentManifest environment)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# CaeriusNet 12.0 benchmark comparison");
        builder.AppendLine();
        builder.Append("- Baseline: `").Append(baselineDirectory).AppendLine("`");
        builder.Append("- Candidate: `").Append(candidateDirectory).AppendLine("`");
        builder.Append("- Comparable environment: `").Append(environment.CompatibilityFingerprint).AppendLine("`");
        builder.Append("- Job: `").Append(environment.Settings.ToDisplayString()).AppendLine("`");
        builder.Append("- Time threshold: ")
            .Append(maximumTimeRegressionPercent.ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine("%");
        builder.AppendLine("- Allocation threshold: no additional bytes per operation");
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

    private static string CreateIncomparableEnvironmentReport(
        string baselineDirectory,
        string candidateDirectory,
        BenchmarkEnvironmentManifest baseline,
        BenchmarkEnvironmentManifest candidate,
        IReadOnlyList<string> differences)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# CaeriusNet 12.0 benchmark comparison not performed");
        builder.AppendLine();
        builder.Append("- Baseline: `").Append(baselineDirectory).AppendLine("`");
        builder.Append("- Candidate: `").Append(candidateDirectory).AppendLine("`");
        builder.Append("- Baseline fingerprint: `").Append(baseline.CompatibilityFingerprint).AppendLine("`");
        builder.Append("- Candidate fingerprint: `").Append(candidate.CompatibilityFingerprint).AppendLine("`");
        builder.AppendLine();
        builder.AppendLine(
            "The time and allocation gate was not run because BenchmarkDotNet measurements from unlike environments are not comparable.");
        builder.AppendLine();
        builder.AppendLine("## Environment differences");
        foreach (var difference in differences)
            builder.Append("- ").AppendLine(difference);
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

    private static int ReadRequiredInt(JsonElement parent, string propertyName, string file)
    {
        return parent.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out var value) && value > 0
            ? value
            : throw new InvalidOperationException($"'{file}' is missing positive integer property '{propertyName}'.");
    }

    private sealed record BenchmarkBundle(
        IReadOnlyDictionary<string, BenchmarkMeasurement> Measurements,
        BenchmarkEnvironmentManifest Manifest);

    private sealed record BenchmarkMeasurement(string Key, double MedianNanoseconds, long AllocatedBytes);

    private sealed record BenchmarkReportSnapshot(string RelativePath, string Sha256, int MeasurementCount);

    private sealed record BenchmarkEnvironmentManifest(
        int SchemaVersion,
        string BenchmarkLine,
        string Suite,
        string CompatibilityFingerprint,
        BenchmarkSettings Settings,
        BenchmarkEnvironment Environment,
        IReadOnlyList<BenchmarkReportSnapshot> Reports);

    private sealed record BenchmarkSettings(
        IReadOnlyList<string> JobSignatures,
        bool MemoryDiagnoserEnabled)
    {
        public string ToDisplayString()
        {
            return $"jobs={string.Join(" | ", JobSignatures)}; memory={MemoryDiagnoserEnabled}";
        }
    }

    private sealed record BenchmarkEnvironment(
        string FrameworkDescription,
        string RuntimeVersion,
        string OperatingSystemDescription,
        string OperatingSystemVersion,
        string OperatingSystemArchitecture,
        string ProcessArchitecture,
        int ProcessorCount,
        string ProcessorModel,
        string BenchmarkDotNetVersion,
        string DotNetSdkVersion,
        string RunnerOperatingSystem,
        string RunnerArchitecture,
        string RunnerImage,
        string RunnerImageVersion);

    private sealed record BenchmarkDotNetHostEnvironment(
        string BenchmarkDotNetVersion,
        string OperatingSystemVersion,
        string ProcessorName,
        int LogicalCoreCount,
        string RuntimeVersion,
        string Architecture,
        string DotNetCliVersion);

    private sealed record ComparisonRow(
        string Key,
        BenchmarkMeasurement Baseline,
        BenchmarkMeasurement Candidate,
        double TimeChangePercent,
        long AllocationChange,
        bool Failed);

    private sealed record Evaluation(IReadOnlyList<ComparisonRow> Rows, IReadOnlyList<string> Failures);
}
