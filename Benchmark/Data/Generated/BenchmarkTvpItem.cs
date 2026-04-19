using CaeriusNet.Attributes.Tvp;

namespace CaeriusNet.Benchmark.Data.Generated;

/// <summary>
///     TVP type used by TVP serialization benchmarks. [GenerateTvp] generates ITvpMapper&lt;T&gt;
///     which produces SqlDataRecord[] for streaming to SQL Server.
/// </summary>
[GenerateTvp(TvpName = "tvp_BenchmarkItem", Schema = "dbo")]
public sealed partial record BenchmarkTvpItem(int Id, string Name, decimal Price);
