using System.Data.SqlTypes;

namespace CaeriusNet.Benchmark.Infrastructure;

/// <summary>
///     Represents a normal SQL Server table-valued parameter row. The generated static writer is used
///     directly by the TVP benchmarks to measure the real record-reuse hot path without opening SQL Server.
/// </summary>
[GenerateTvp(Schema = "dbo", TvpName = "BenchmarkItemType")]
public sealed partial record BenchmarkTvpRow(
    [SqlServerTvpColumn("Id", 0, SqlDbType.Int, false)]
    int Id,
    [SqlServerTvpColumn(
        "Name",
        1,
        SqlDbType.NVarChar,
        false,
        MaxLength = 128,
        LocaleId = 1033,
        CompareOptions = SqlCompareOptions.None)]
    string Name,
    [SqlServerTvpColumn("Amount", 2, SqlDbType.Decimal, false, Precision = 18, Scale = 4)]
    decimal Amount,
    [SqlServerTvpColumn("CreatedAt", 3, SqlDbType.DateTime2, false, Scale = 7)]
    DateTime CreatedAt);
