namespace CaeriusNet.Benchmark.Data.Generated;

/// <summary>
///     Ten-column TVP type used by TVP column-scaling benchmarks.
///     Represents a realistic wide-row TVP (mixed types: int, string, decimal, bool, DateTime, Guid).
///     Measures the per-column SetXxx cost on the reused SqlDataRecord instance.
/// </summary>
[GenerateTvp(TvpName = "tvp_BenchmarkItem10Col", Schema = "dbo")]
public sealed partial record BenchmarkTvpItem10Col(
    int Id,
    string Name,
    decimal Price,
    bool IsActive,
    DateTime CreatedDate,
    string Category,
    int Quantity,
    decimal Score,
    string Description,
    Guid TraceId);
