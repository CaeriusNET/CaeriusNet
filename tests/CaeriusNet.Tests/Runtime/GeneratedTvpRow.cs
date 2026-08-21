using System.Data;

namespace CaeriusNet.Tests.Runtime;

[GenerateTvp(Schema = "dbo", TvpName = "GeneratedRows")]
internal sealed partial record GeneratedTvpRow(
    [SqlServerTvpColumn("Value", 0, SqlDbType.Int, false)]
    int Value,
    [SqlServerTvpColumn("OccurredAt", 1, SqlDbType.DateTime2, false, Scale = 7)]
    DateTime OccurredAt = default,
    [SqlServerTvpColumn("LocalTime", 2, SqlDbType.Time, false, Scale = 7)]
    TimeOnly LocalTime = default,
    [SqlServerTvpColumn("OffsetTime", 3, SqlDbType.DateTimeOffset, false, Scale = 7)]
    DateTimeOffset OffsetTime = default);
