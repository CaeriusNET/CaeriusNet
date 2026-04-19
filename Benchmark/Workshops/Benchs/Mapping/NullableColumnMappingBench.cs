using CaeriusNet.Benchmark.Data.Generated;

namespace CaeriusNet.Benchmark.Workshops.Benchs.Mapping;

/// <summary>
///     Benchmarks nullable-column reading patterns used in <c>MapFromDataReader</c>.
/// </summary>
/// <remarks>
///     <para>
///         The source-generated mapper for <see cref="NullableRowDto" /> emits:
///         <c>reader.IsDBNull(i) ? null : reader.GetXxx(i)</c> for each nullable field.
///         This benchmark isolates the cost of that IsDBNull branch (true = DBNull, false = value)
///         against a non-nullable baseline (no DBNull check at all).
///     </para>
///     <para>
///         Real-world data typically has ~20–50% null values for optional fields.
///         We test three null densities: 0%, 50%, and 100% to profile the branch predictor impact.
///     </para>
/// </remarks>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.CacheMisses)]
public class NullableColumnMappingBench
{
    // Non-nullable source arrays (5-col DTO, no DBNull checks needed)
    private int[] _ids = null!;
    private bool[] _isActives = null!;
    private string[] _names = null!;
    private DateTime?[] _nullableCreatedAts = null!;
    private bool?[] _nullableIsActives = null!;

    // Nullable source arrays (NullableRowDto)
    private string?[] _nullableNames = null!;
    private decimal?[] _nullablePrices = null!;
    private decimal[] _prices = null!;
    private Guid[] _traceIds = null!;

    [Params(100, 1_000, 10_000)] public int RowCount { get; set; }

    /// <summary>
    ///     Null density in nullable columns: 0 = no nulls, 50 = ~50% nulls, 100 = all nulls.
    /// </summary>
    [Params(0, 50, 100)]
    public int NullPercent { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        _ids = new int[RowCount];
        _traceIds = new Guid[RowCount];
        _names = new string[RowCount];
        _prices = new decimal[RowCount];
        _isActives = new bool[RowCount];
        _nullableNames = new string?[RowCount];
        _nullablePrices = new decimal?[RowCount];
        _nullableIsActives = new bool?[RowCount];
        _nullableCreatedAts = new DateTime?[RowCount];

        for (var i = 0; i < RowCount; i++)
        {
            _ids[i] = i + 1;
            _traceIds[i] = Guid.NewGuid();
            _names[i] = $"item_{i:D6}";
            _prices[i] = Math.Round((decimal)(rng.NextDouble() * 9999), 2);
            _isActives[i] = i % 2 == 0;

            var isNull = rng.Next(100) < NullPercent;
            _nullableNames[i] = isNull ? null : $"item_{i:D6}";
            _nullablePrices[i] = isNull ? null : Math.Round((decimal)(rng.NextDouble() * 9999), 2);
            _nullableIsActives[i] = isNull ? null : i % 2 == 0;
            _nullableCreatedAts[i] = isNull ? null : DateTime.UtcNow.AddSeconds(-rng.Next(86400));
        }
    }

    /// <summary>
    ///     Baseline: construct fully non-nullable <see cref="BenchmarkItemDto" /> — no DBNull checks.
    ///     Represents the best-case mapper (all columns guaranteed non-null).
    /// </summary>
    [Benchmark(Baseline = true, Description = "Non-nullable DTO: no IsDBNull checks")]
    public BenchmarkItemDto[] Map_NonNullable_DTO()
    {
        var result = new BenchmarkItemDto[RowCount];
        for (var i = 0; i < RowCount; i++)
            result[i] = new BenchmarkItemDto(_ids[i], _traceIds[i], _names[i], _prices[i], _isActives[i]);
        return result;
    }

    /// <summary>
    ///     Nullable DTO: mirrors the source-generated IsDBNull ternary for every nullable field.
    ///     4 nullable fields × RowCount checks.
    /// </summary>
    [Benchmark(Description = "Nullable DTO: IsDBNull ternary (source-gen pattern)")]
    public NullableRowDto[] Map_Nullable_DTO_IsDBNull_Ternary()
    {
        var result = new NullableRowDto[RowCount];
        for (var i = 0; i < RowCount; i++)
            result[i] = new NullableRowDto(
                _ids[i],
                _nullableNames[i], // mirrors: reader.IsDBNull(1) ? null : reader.GetString(1)
                _nullablePrices[i], // mirrors: reader.IsDBNull(2) ? null : reader.GetDecimal(2)
                _nullableIsActives[i], // mirrors: reader.IsDBNull(3) ? null : reader.GetBoolean(3)
                _nullableCreatedAts[i] // mirrors: reader.IsDBNull(4) ? null : reader.GetDateTime(4)
            );
        return result;
    }

    /// <summary>
    ///     Eager-check variant: check all 4 nulls upfront before construction.
    ///     Tests whether branch grouping helps the branch predictor.
    /// </summary>
    [Benchmark(Description = "Nullable DTO: upfront null check then construct")]
    public NullableRowDto[] Map_Nullable_DTO_Upfront_Check()
    {
        var result = new NullableRowDto[RowCount];
        for (var i = 0; i < RowCount; i++)
        {
            var name = _nullableNames[i];
            var price = _nullablePrices[i];
            var active = _nullableIsActives[i];
            var createdAt = _nullableCreatedAts[i];
            result[i] = new NullableRowDto(_ids[i], name, price, active, createdAt);
        }

        return result;
    }
}