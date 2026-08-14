using CaeriusNet;

namespace CaeriusNet.Benchmark.Infrastructure;

/// <summary>
///     Small representative result row. The source generator emits both the data-reader mapper and
///     its deterministic binary cache codec, so the benchmark also serves as a compile-time smoke test.
/// </summary>
[GenerateDto]
public sealed partial record BenchmarkDto(
    int Id,
    string Name,
    decimal Amount,
    DateTimeOffset ChangedAt,
    bool Enabled,
    string? OptionalName);