using CaeriusNet.Benchmark.Infrastructure;
using CaeriusNet.Mappers;

namespace CaeriusNet.Benchmark.TableValuedParameters;

[MemoryDiagnoser]
[BenchmarkCategory("tvp")]
public class TvpBenchmarks : IDisposable
{
    private SqlConnection _connection = null!;
    private StoredProcedureCommand _replayableCommand = null!;
    private BenchmarkTvpRow[] _rows = [];
    private StoredProcedureCommand _streamingCommand = null!;

    /// <summary>Exercises empty/small, common bulk and genuinely large TVP streams.</summary>
    [Params(0, 1, 1_000, 100_000)]
    public int RowCount { get; set; }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

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

        // SqlCommand population is local provider work; no connection is opened in these benchmarks.
        _connection = new SqlConnection();
        _replayableCommand = new StoredProcedureCommandBuilder("dbo", "BenchmarkTvp")
            .AddTvpParameter("@items", _rows)
            .Build();
        _streamingCommand = new StoredProcedureCommandBuilder("dbo", "BenchmarkTvp")
            .AddTvpParameter("@items", CreateRows)
            .Build();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Dispose();
    }

    /// <summary>
    ///     Measures the generated <see cref="ITvpMapper{T}.WriteRow" /> code while reusing one record,
    ///     exactly as the runtime does when it enumerates a structured parameter.
    /// </summary>
    [Benchmark]
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

    /// <summary>
    ///     Creates the provider command then logically enumerates the structured value, which is the same generated
    ///     record-reuse path SqlClient consumes while sending a replayable TVP. The collection remains unmaterialized.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int BindAndEnumerateReplayableTvp()
    {
        return BindAndEnumerate(_replayableCommand);
    }

    /// <summary>
    ///     Covers the explicit factory-based streaming TVP API. It deliberately shares the rows with the replayable
    ///     case, so the comparison isolates factory/prefetch binding rather than data shape or a SQL Server round trip.
    /// </summary>
    [Benchmark]
    public int BindAndEnumerateStreamingTvp()
    {
        return BindAndEnumerate(_streamingCommand);
    }

    private BenchmarkTvpRow[] CreateRows(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _rows;
    }

    private int BindAndEnumerate(StoredProcedureCommand storedProcedureCommand)
    {
        using var command = storedProcedureCommand.CreateSqlCommand(_connection);
        if (command.Parameters[0].Value is not IEnumerable<SqlDataRecord> records)
            return 0;

        var checksum = 17;
        foreach (var record in records)
            checksum = unchecked(checksum * 31 + record.GetInt32(0));
        return checksum;
    }
}
