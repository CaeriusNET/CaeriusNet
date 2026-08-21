using Xunit;

namespace CaeriusNet.Tests.Observability;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TelemetryTestScope
{
    public const string Name = "CaeriusNet telemetry";
}
