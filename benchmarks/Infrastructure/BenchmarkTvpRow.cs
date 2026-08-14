using CaeriusNet;

namespace CaeriusNet.Benchmark.Infrastructure;

/// <summary>
///     Represents a normal SQL Server table-valued parameter row. The generated static writer is used
///     directly by the TVP benchmarks to measure the real record-reuse hot path without opening SQL Server.
/// </summary>
[GenerateTvp(Schema = "dbo", TvpName = "BenchmarkItemType")]
public sealed partial record BenchmarkTvpRow(
    int Id,
    string Name,
    decimal Amount,
    DateTime CreatedAt);