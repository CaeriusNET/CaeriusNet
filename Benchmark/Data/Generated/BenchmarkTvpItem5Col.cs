using CaeriusNet.Attributes.Tvp;

namespace CaeriusNet.Benchmark.Data.Generated;

/// <summary>
///     Five-column TVP type used by TVP column-scaling benchmarks.
///     Measures serialization cost growth as the column count increases from 3 → 5 → 10.
/// </summary>
[GenerateTvp(TvpName = "tvp_BenchmarkItem5Col", Schema = "dbo")]
public sealed partial record BenchmarkTvpItem5Col(
    int Id,
    string Name,
    decimal Price,
    bool IsActive,
    DateTime CreatedDate);
