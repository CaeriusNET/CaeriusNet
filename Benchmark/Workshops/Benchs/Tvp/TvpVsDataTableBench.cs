using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.Tvp;

/// <summary>
///     Compares CaeriusNet's lazy <see cref="SqlDataRecord" /> streaming approach
///     against a traditional <see cref="DataTable" /> materialisation.
/// </summary>
/// <remarks>
///     <para>
///         CaeriusNet's source-generated <c>MapAsSqlDataRecords()</c> allocates a single
///         <see cref="SqlDataRecord" /> instance for the entire enumeration — O(1) allocation
///         regardless of row count. <see cref="DataTable" /> allocates O(rows × columns) objects
///         (one <see cref="DataRow" /> per row, plus boxing for every value).
///     </para>
///     <para>
///         This benchmark quantifies that allocation advantage and demonstrates why CaeriusNet's
///         TVP streaming approach is superior for large batches.
///     </para>
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.CacheMisses)]
public class TvpVsDataTableBench
{
    private static readonly Faker<BenchmarkTvpItem> Faker = new Faker<BenchmarkTvpItem>()
        .CustomInstantiator(f => new BenchmarkTvpItem(
            f.Random.Int(1, 100_000),
            f.Internet.UserName(),
            Math.Round((decimal)f.Random.Double(0.01, 9999.99), 2)));

    // Reusable DataTable schema — avoids per-iteration column-add overhead
    private DataTable _dataTableSchema = null!;

    private List<BenchmarkTvpItem> _items = null!;

    [Params(10, 100, 1_000, 10_000, 50_000, 100_000)] public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        Randomizer.Seed = new Random(42);
        _items = Faker.Generate(RowCount);

        _dataTableSchema = new DataTable("tvp_BenchmarkItem");
        _dataTableSchema.Columns.Add("Id", typeof(int));
        _dataTableSchema.Columns.Add("Name", typeof(string));
        _dataTableSchema.Columns.Add("Price", typeof(decimal));
    }

    /// <summary>
    ///     CaeriusNet approach: single <see cref="SqlDataRecord" /> instance reused per row.
    ///     Allocation is constant (O(1)) — only the iterator state machine and the record object.
    /// </summary>
    [Benchmark(Baseline = true, Description = "CaeriusNet: lazy SqlDataRecord stream (O(1) alloc)")]
    public int CaeriusNet_LazyStream_Enumerate()
    {
        var firstItem = _items[0];
        var records = firstItem.MapAsSqlDataRecords(_items);
        var count = 0;
        foreach (var _ in records)
            count++;
        return count;
    }

    /// <summary>
    ///     Traditional DataTable approach: allocates one <see cref="DataRow" /> per item
    ///     plus boxing overhead for each value. O(N) allocation.
    /// </summary>
    [Benchmark(Description = "DataTable: one DataRow per item + boxing (O(N) alloc)")]
    public int DataTable_FillRows()
    {
        var dt = _dataTableSchema.Clone(); // Clone schema (columns), no rows
        foreach (var item in _items)
        {
            var row = dt.NewRow();
            row[0] = item.Id;
            row[1] = item.Name;
            row[2] = item.Price;
            dt.Rows.Add(row);
        }

        return dt.Rows.Count;
    }

    /// <summary>
    ///     DataTable with <see cref="DataTable.BeginLoadData" /> / <see cref="DataTable.EndLoadData" />
    ///     to disable index rebuilding during fill — the common optimisation tip.
    ///     Still O(N) allocation, but faster constraint evaluation.
    /// </summary>
    [Benchmark(Description = "DataTable: BeginLoadData/EndLoadData optimised fill")]
    public int DataTable_FillRows_BeginLoadData()
    {
        var dt = _dataTableSchema.Clone();
        dt.BeginLoadData();
        foreach (var item in _items)
        {
            var row = dt.NewRow();
            row[0] = item.Id;
            row[1] = item.Name;
            row[2] = item.Price;
            dt.Rows.Add(row);
        }

        dt.EndLoadData();
        return dt.Rows.Count;
    }
}