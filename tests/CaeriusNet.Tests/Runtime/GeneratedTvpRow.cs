using CaeriusNet;

namespace CaeriusNet.Tests.Runtime;

[GenerateTvp(Schema = "dbo", TvpName = "GeneratedRows")]
internal sealed partial record GeneratedTvpRow(
    int Value,
    DateTime OccurredAt = default,
    TimeOnly LocalTime = default,
    DateTimeOffset OffsetTime = default);