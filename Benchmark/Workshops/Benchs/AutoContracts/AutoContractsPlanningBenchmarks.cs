using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace CaeriusNet.Benchmark.Workshops.Benchs.AutoContracts;

/// <summary>
///     Measures the generated AutoContracts parameter-builder facade.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public sealed class AutoContractsBuilderBenchmarks
{
    private static readonly BenchmarkProcedureParameters Parameters =
        new(42, Guid.Parse("7e85a27d-73ba-4a20-98a9-2b3f57200d1d"), "benchmark", 12.50m, true);

    [Benchmark(Baseline = true, Description = "Untyped StoredProcedureParametersBuilder")]
    public StoredProcedureParameters UntypedParameterBuilder()
    {
        return new StoredProcedureParametersBuilder("dbo", "usp_AutoContracts_Benchmark")
            .AddParameter("@Id", Parameters.Id, SqlDbType.Int)
            .AddParameter("@TraceId", Parameters.TraceId, SqlDbType.UniqueIdentifier)
            .AddParameter("@Name", Parameters.Name, SqlDbType.NVarChar)
            .AddParameter("@Amount", Parameters.Amount, SqlDbType.Decimal)
            .AddParameter("@Active", Parameters.Active, SqlDbType.Bit)
            .Build();
    }

    [Benchmark(Description = "Typed generated parameter builder")]
    public StoredProcedureParameters<BenchmarkProcedure> TypedGeneratedParameterBuilder()
    {
        return StoredProcedureParametersBuilder<BenchmarkProcedure>
            .Create(16)
            .WithParameters(Parameters)
            .Build();
    }

    [Benchmark(Description = "Typed generated parameter builder with cache key")]
    public StoredProcedureParameters<BenchmarkProcedure> TypedGeneratedParameterBuilderWithCacheKey()
    {
        return StoredProcedureParametersBuilder<BenchmarkProcedure>
            .Create(16)
            .WithParameters(Parameters)
            .AddFrozenCache(BenchmarkProcedureCacheKeys.ByParameters(Parameters))
            .Build();
    }
}

/// <summary>
///     Measures generated AutoContracts hot paths that run after SQL execution planning.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public sealed class AutoContractsGeneratedHotPathBenchmarks
{
    private int[] _ids = null!;
    private bool[] _isActives = null!;
    private string[] _names = null!;
    private BenchmarkProcedureParameters[] _parameters = null!;
    private decimal[] _prices = null!;
    private Guid[] _traceIds = null!;

    [Params(1, 100, 1_000, 10_000)] public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _ids = new int[RowCount];
        _traceIds = new Guid[RowCount];
        _names = new string[RowCount];
        _prices = new decimal[RowCount];
        _isActives = new bool[RowCount];
        _parameters = new BenchmarkProcedureParameters[RowCount];

        for (var i = 0; i < RowCount; i++)
        {
            _ids[i] = random.Next(1, 100_000);
            _traceIds[i] = Guid.NewGuid();
            _names[i] = "user_" + i;
            _prices[i] = Math.Round((decimal)(random.NextDouble() * 9999), 2);
            _isActives[i] = i % 2 == 0;
            _parameters[i] = new BenchmarkProcedureParameters(
                _ids[i],
                _traceIds[i],
                _names[i],
                _prices[i],
                _isActives[i]);
        }
    }

    [Benchmark(Baseline = true, Description = "Generated ordinal result mapping")]
    public BenchmarkResultRow[] GeneratedOrdinalResultMapping()
    {
        var result = new BenchmarkResultRow[RowCount];
        for (var row = 0; row < RowCount; row++)
            result[row] = BenchmarkResultRowMapper.Map(new OrdinalRowReader(
                row,
                _ids,
                _traceIds,
                _names,
                _prices,
                _isActives));

        return result;
    }

    [Benchmark(Description = "Generated cache key hashing")]
    public string[] GeneratedCacheKeyHashing()
    {
        var result = new string[RowCount];
        for (var i = 0; i < RowCount; i++)
            result[i] = BenchmarkProcedureCacheKeys.ByParameters(_parameters[i]);

        return result;
    }
}

public readonly struct BenchmarkProcedure : ICaeriusGeneratedProcedure<BenchmarkProcedure>
{
    public static string SchemaName => "dbo";
    public static string ProcedureName => "usp_AutoContracts_Benchmark";
    public static string FullName => "dbo.usp_AutoContracts_Benchmark";
    public static string ContractHash => "sha256:benchmark";
    public static int ParameterCount => 5;
    public static int ResultSetCount => 1;
}

public sealed record BenchmarkProcedureParameters(
    int Id,
    Guid TraceId,
    string Name,
    decimal Amount,
    bool Active);

public readonly record struct BenchmarkResultRow(
    int Id,
    Guid TraceId,
    string Name,
    decimal Amount,
    bool Active);

public static class BenchmarkProcedureBuilderExtensions
{
    public static StoredProcedureParametersBuilder<BenchmarkProcedure> WithParameters(
        this StoredProcedureParametersBuilder<BenchmarkProcedure> builder,
        BenchmarkProcedureParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(parameters);

        return builder
            .AddGeneratedParameter(1, "@Id", parameters.Id, SqlDbType.Int)
            .AddGeneratedParameter(2, "@TraceId", parameters.TraceId, SqlDbType.UniqueIdentifier)
            .AddGeneratedParameter(3, "@Name", parameters.Name, SqlDbType.NVarChar, 64)
            .AddGeneratedParameter(4, "@Amount", parameters.Amount, SqlDbType.Decimal, precision: 18, scale: 2)
            .AddGeneratedParameter(5, "@Active", parameters.Active, SqlDbType.Bit)
            .MarkGeneratedParametersBound();
    }
}

public static class BenchmarkResultRowMapper
{
    public static BenchmarkResultRow Map(OrdinalRowReader reader)
    {
        return new BenchmarkResultRow(
            reader.GetInt32(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetBoolean(4));
    }
}

public readonly ref struct OrdinalRowReader(
    int row,
    int[] ids,
    Guid[] traceIds,
    string[] names,
    decimal[] amounts,
    bool[] actives)
{
    public int GetInt32(int ordinal)
    {
        return ordinal == 0 ? ids[row] : throw new ArgumentOutOfRangeException(nameof(ordinal));
    }

    public Guid GetGuid(int ordinal)
    {
        return ordinal == 1 ? traceIds[row] : throw new ArgumentOutOfRangeException(nameof(ordinal));
    }

    public string GetString(int ordinal)
    {
        return ordinal == 2 ? names[row] : throw new ArgumentOutOfRangeException(nameof(ordinal));
    }

    public decimal GetDecimal(int ordinal)
    {
        return ordinal == 3 ? amounts[row] : throw new ArgumentOutOfRangeException(nameof(ordinal));
    }

    public bool GetBoolean(int ordinal)
    {
        return ordinal == 4 ? actives[row] : throw new ArgumentOutOfRangeException(nameof(ordinal));
    }
}

public static class BenchmarkProcedureCacheKeys
{
    public static string ByParameters(BenchmarkProcedureParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, BenchmarkProcedure.FullName);
        AppendString(hash, BenchmarkProcedure.ContractHash);

        AppendString(hash, "@Id");
        AppendInt32(hash, parameters.Id);
        AppendString(hash, "@TraceId");
        AppendGuid(hash, parameters.TraceId);
        AppendString(hash, "@Name");
        AppendString(hash, parameters.Name);
        AppendString(hash, "@Amount");
        AppendDecimal(hash, parameters.Amount);
        AppendString(hash, "@Active");
        AppendBoolean(hash, parameters.Active);

        return BenchmarkProcedure.FullName + ":" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendBoolean(IncrementalHash hash, bool value)
    {
        AppendByte(hash, value ? (byte)1 : (byte)0);
    }

    private static void AppendByte(IncrementalHash hash, byte value)
    {
        Span<byte> buffer = stackalloc byte[1];
        buffer[0] = value;
        hash.AppendData(buffer);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        hash.AppendData(buffer);
    }

    private static void AppendGuid(IncrementalHash hash, Guid value)
    {
        Span<byte> buffer = stackalloc byte[16];
        value.TryWriteBytes(buffer);
        hash.AppendData(buffer);
    }

    private static void AppendDecimal(IncrementalHash hash, decimal value)
    {
        foreach (var part in decimal.GetBits(value))
            AppendInt32(hash, part);
    }

    private static void AppendString(IncrementalHash hash, string? value)
    {
        if (value is null)
        {
            AppendByte(hash, 0);
            return;
        }

        AppendByte(hash, 1);
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }
}