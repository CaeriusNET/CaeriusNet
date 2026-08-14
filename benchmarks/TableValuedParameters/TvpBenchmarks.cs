using CaeriusNet.Benchmark.Infrastructure;
using CaeriusNet.Mappers;

namespace CaeriusNet.Benchmark.TableValuedParameters;

[MemoryDiagnoser]
[WarmupCount(2)]
[IterationCount(6)]
[BenchmarkCategory("tvp")]
public class TvpBenchmarks
{
    private BenchmarkTvpRow[] _rows = [];

    /// <summary>Exercises empty/small, common bulk and genuinely large TVP streams.</summary>
    [Params(0, 1, 1_000, 100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _rows = new BenchmarkTvpRow[RowCount];
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < _rows.Length; index++)
            _rows[index] = new BenchmarkTvpRow(
                index,
                "row-" + index.ToString(CultureInfo.InvariantCulture),
                index / 100m,
                start.AddSeconds(index));
    }

    /// <summary>
    ///     Measures the generated <see cref="ITvpMapper{T}.WriteRow" /> code while reusing one record,
    ///     exactly as the runtime does when it enumerates a structured parameter.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int WriteRowsIntoOneReusableSqlDataRecord()
    {
        var record = BenchmarkTvpRow.CreateRecord();
        var checksum = 17;
        foreach (var row in _rows)
        {
            BenchmarkTvpRow.WriteRow(record, row);
            checksum = unchecked(checksum * 31 + record.GetInt32(0));
        }

        return checksum;
    }

    [Benchmark]
    public StoredProcedureCommand BuildStructuredParameterDescriptor()
    {
        return new StoredProcedureCommandBuilder("dbo", "BenchmarkTvp")
            .AddTvpParameter("@items", _rows)
            .Build();
    }
}
