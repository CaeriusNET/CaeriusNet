using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.Parameters;

/// <summary>
///     Benchmarks <see cref="StoredProcedureParametersBuilder.AddTvpParameter{T}" /> with
///     <c>List&lt;T&gt;</c> vs non-list <c>IEnumerable&lt;T&gt;</c> input.
/// </summary>
/// <remarks>
///     <para>
///         The builder contains an internal fast-path:
///         <c>var tvpList = items is IList&lt;T&gt; list ? list : items.ToList();</c>
///         When <paramref name="items" /> is already a <see cref="List{T}" />, the cast succeeds —
///         zero allocation, O(1). When it is a lazy <c>IEnumerable&lt;T&gt;</c> (e.g. a LINQ chain),
///         the builder must materialise it via <c>.ToList()</c>, which allocates a new backing array
///         and copies all elements — O(N) allocation.
///     </para>
///     <para>
///         This benchmark quantifies that difference and demonstrates why callers should always
///         prefer passing a pre-materialised <see cref="List{T}" /> (or <see cref="IList{T}" />)
///         to <c>AddTvpParameter</c>.
///     </para>
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class AddTvpParameterBench
{
    private static readonly Faker<BenchmarkTvpItem> Faker = new Faker<BenchmarkTvpItem>()
        .CustomInstantiator(f => new BenchmarkTvpItem(
            f.Random.Int(1, 100_000),
            f.Internet.UserName(),
            Math.Round((decimal)f.Random.Double(0.01, 9999.99), 2)));

    // Non-list IEnumerable backed by an array — triggers .ToList() inside AddTvpParameter
    private IEnumerable<BenchmarkTvpItem> _itemEnumerable = null!;

    private List<BenchmarkTvpItem> _itemList = null!;

    [Params(10, 100, 1_000)] public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        Randomizer.Seed = new Random(42);
        _itemList = Faker.Generate(RowCount);
        // Use .Where(_ => true) to ensure the type is NOT IList<T> — forces .ToList() in builder
        _itemEnumerable = _itemList.Where(_ => true);
    }

    /// <summary>
    ///     Fast path: <c>List&lt;T&gt; is IList&lt;T&gt;</c> → direct cast, zero allocation.
    ///     <c>MapAsSqlDataRecords</c> iterator is created but NOT enumerated (lazy).
    /// </summary>
    [Benchmark(Baseline = true, Description = "AddTvpParameter<T>: List<T> fast path (O(1) alloc)")]
    public StoredProcedureParameters AddTvpParameter_List()
    {
        return new StoredProcedureParametersBuilder("dbo", "usp_InsertBenchmarkItemsBatch")
            .AddTvpParameter("@Items", _itemList)
            .Build();
    }

    /// <summary>
    ///     Slow path: non-list <c>IEnumerable&lt;T&gt;</c> → builder calls <c>.ToList()</c>,
    ///     allocating a new backing array and copying all <paramref name="RowCount" /> elements.
    /// </summary>
    [Benchmark(Description = "AddTvpParameter<T>: IEnumerable<T> slow path (.ToList() alloc)")]
    public StoredProcedureParameters AddTvpParameter_IEnumerable()
    {
        return new StoredProcedureParametersBuilder("dbo", "usp_InsertBenchmarkItemsBatch")
            .AddTvpParameter("@Items", _itemEnumerable)
            .Build();
    }

    /// <summary>
    ///     Informational: multiple TVP parameters in one builder call (N inserts pattern vs batch).
    ///     Both use <c>List&lt;T&gt;</c> to avoid the IEnumerable penalty.
    /// </summary>
    [Benchmark(Description = "Two AddTvpParameter<T> calls in one builder (batched parameters)")]
    public StoredProcedureParameters AddTvpParameter_TwoTvps()
    {
        return new StoredProcedureParametersBuilder("dbo", "usp_MergeBenchmarkItems")
            .AddTvpParameter("@NewItems", _itemList)
            .AddTvpParameter("@ExistingItems", _itemList)
            .Build();
    }
}