using BenchmarkDotNet.Attributes;
using Bogus;
using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.Mapping;

/// <summary>
///     Benchmarks the positional-constructor mapping cost for DTOs with 5 vs 10 columns.
/// </summary>
/// <remarks>
///     <para>
///         The source-generated <c>MapFromDataReader()</c> calls one typed <c>reader.GetXxx(ordinal)</c>
///         per column and passes all values to a positional record constructor.
///         This benchmark isolates the <em>column-count factor</em> — how the construction cost
///         and allocation grow as the DTO width increases.
///     </para>
///     <para>
///         Since <c>SqlDataReader</c> cannot be instantiated offline, we simulate the mapping work
///         by reading from pre-populated typed arrays, which mirrors the exact operations the
///         generated mapper performs (no reader overhead, pure construction cost).
///     </para>
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.CacheMisses)]
public class WideRowDtoMappingBench
{
    // 5-col source arrays (mirrors BenchmarkItemDto fields)
    private int[] _ids = null!;
    private Guid[] _traceIds = null!;
    private string[] _names = null!;
    private decimal[] _prices = null!;
    private bool[] _isActives = null!;

    // 10-col extra arrays (mirrors WideRowDto extra fields)
    private DateTime[] _fetchedAts = null!;
    private int[] _categories = null!;
    private int[] _quantities = null!;
    private decimal[] _pricesWithTax = null!;
    private decimal[] _discountedPrices = null!;
    private bool[] _inStocks = null!;

    [Params(1, 100, 1_000, 10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        _ids = new int[RowCount];
        _traceIds = new Guid[RowCount];
        _names = new string[RowCount];
        _prices = new decimal[RowCount];
        _isActives = new bool[RowCount];
        _fetchedAts = new DateTime[RowCount];
        _categories = new int[RowCount];
        _quantities = new int[RowCount];
        _pricesWithTax = new decimal[RowCount];
        _discountedPrices = new decimal[RowCount];
        _inStocks = new bool[RowCount];

        for (var i = 0; i < RowCount; i++)
        {
            _ids[i] = rng.Next(1, 100_000);
            _traceIds[i] = Guid.NewGuid();
            _names[i] = $"item_{i:D6}";
            _prices[i] = Math.Round((decimal)(rng.NextDouble() * 9999), 2);
            _isActives[i] = i % 2 == 0;
            _fetchedAts[i] = DateTime.UtcNow.AddSeconds(-rng.Next(0, 86400));
            _categories[i] = rng.Next(1, 20);
            _quantities[i] = rng.Next(1, 500);
            _pricesWithTax[i] = Math.Round(_prices[i] * 1.2m, 2);
            _discountedPrices[i] = Math.Round(_prices[i] * 0.9m, 2);
            _inStocks[i] = i % 3 != 0;
        }
    }

    /// <summary>
    ///     5-column DTO: mirrors <see cref="BenchmarkItemDto" />.
    ///     Positional constructor with 5 strongly-typed arguments.
    /// </summary>
    [Benchmark(Baseline = true, Description = "5-col DTO: positional ctor (List)")]
    public List<BenchmarkItemDto> Map_5Column_DTO_ToList()
    {
        var result = new List<BenchmarkItemDto>(RowCount);
        for (var i = 0; i < RowCount; i++)
            result.Add(new BenchmarkItemDto(_ids[i], _traceIds[i], _names[i], _prices[i], _isActives[i]));
        return result;
    }

    /// <summary>
    ///     10-column DTO: mirrors <see cref="WideRowDto" />.
    ///     Positional constructor with 10 strongly-typed arguments — twice the set calls.
    /// </summary>
    [Benchmark(Description = "10-col DTO: positional ctor (List)")]
    public List<WideRowDto> Map_10Column_DTO_ToList()
    {
        var result = new List<WideRowDto>(RowCount);
        for (var i = 0; i < RowCount; i++)
            result.Add(new WideRowDto(
                _ids[i], _names[i], _prices[i], _isActives[i], _fetchedAts[i],
                _categories[i], _quantities[i], _pricesWithTax[i], _discountedPrices[i], _inStocks[i]));
        return result;
    }

    /// <summary>
    ///     5-column DTO pre-allocated as array — avoids <c>List&lt;T&gt;</c> internal doubling.
    /// </summary>
    [Benchmark(Description = "5-col DTO: pre-allocated array")]
    public BenchmarkItemDto[] Map_5Column_DTO_ToArray()
    {
        var result = new BenchmarkItemDto[RowCount];
        for (var i = 0; i < RowCount; i++)
            result[i] = new BenchmarkItemDto(_ids[i], _traceIds[i], _names[i], _prices[i], _isActives[i]);
        return result;
    }

    /// <summary>
    ///     10-column DTO pre-allocated as array.
    /// </summary>
    [Benchmark(Description = "10-col DTO: pre-allocated array")]
    public WideRowDto[] Map_10Column_DTO_ToArray()
    {
        var result = new WideRowDto[RowCount];
        for (var i = 0; i < RowCount; i++)
            result[i] = new WideRowDto(
                _ids[i], _names[i], _prices[i], _isActives[i], _fetchedAts[i],
                _categories[i], _quantities[i], _pricesWithTax[i], _discountedPrices[i], _inStocks[i]);
        return result;
    }
}
