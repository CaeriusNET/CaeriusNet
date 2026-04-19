namespace CaeriusNet.Benchmark.Data.Generated;

/// <summary>
///     Ten-column DTO used by wide-row mapping benchmarks.
///     Measures how the source-generated <c>MapFromDataReader</c> scales with more columns —
///     each additional column adds one typed <c>reader.GetXxx(ordinal)</c> call.
/// </summary>
[GenerateDto]
public sealed partial record WideRowDto(
    int Id,
    string Name,
    decimal Price,
    bool IsActive,
    DateTime FetchedAt,
    int Category,
    int Quantity,
    decimal PriceWithTax,
    decimal DiscountedPrice,
    bool InStock);