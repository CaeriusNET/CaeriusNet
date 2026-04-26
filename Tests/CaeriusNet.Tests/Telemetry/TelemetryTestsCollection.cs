namespace CaeriusNet.Tests.Telemetry;

/// <summary>
///     xUnit collection marker for all test classes that read or mutate the process-wide static
///     <see cref="CaeriusDiagnostics.TelemetryOptions" />.
///     Tests in this collection are serialised (never run in parallel with each other),
///     preventing races where one test's option change interferes with another test's assertions.
/// </summary>
[CollectionDefinition(Name)]
public sealed class TelemetryTestsCollection
{
    /// <summary>Collection name referenced by <see cref="CollectionAttribute" />.</summary>
    public const string Name = "TelemetryTests";
}
