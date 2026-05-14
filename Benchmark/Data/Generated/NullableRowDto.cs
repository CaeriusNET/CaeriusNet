namespace CaeriusNet.Benchmark.Data.Generated;

/// <summary>
///     DTO with all-nullable fields (except <c>Id</c>) used by nullable-column mapping benchmarks.
///     The source generator emits <c>reader.IsDBNull(i) ? null : reader.GetXxx(i)</c> for each nullable field,
///     enabling a precise measurement of the DBNull-check overhead vs a fully non-nullable DTO.
/// </summary>
[GenerateDto]
public sealed partial record NullableRowDto(
    int Id,
    string? Name,
    decimal? Price,
    bool? IsActive,
    DateTime? CreatedAt);