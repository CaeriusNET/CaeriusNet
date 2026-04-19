using CaeriusNet.Attributes.Dto;

namespace CaeriusNet.Benchmark.Data.Generated;

/// <summary>
///     DTO used by DTO-mapping benchmarks. [GenerateDto] triggers compile-time generation
///     of ISpMapper&lt;BenchmarkItemDto&gt;.MapFromDataReader().
/// </summary>
[GenerateDto]
public sealed partial record BenchmarkItemDto(int Id, Guid TraceId, string Name, decimal Price, bool IsActive);
