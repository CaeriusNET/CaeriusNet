using BenchmarkDotNet.Attributes;
using CaeriusNet.Builders;

namespace CaeriusNet.Benchmark.Workshops.Benchs.Parameters;

/// <summary>
///     Benchmarks the construction of StoredProcedureParametersBuilder with varying parameter counts.
///     Measures the allocation cost of the internal List&lt;SqlParameter&gt; and the Build() call overhead.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class SpParameterBuilderBench
{
    [Params(1, 5, 10, 20)]
    public int ParameterCount { get; set; }

    [Benchmark(Baseline = true, Description = "Build with N int parameters")]
    public StoredProcedureParameters Build_WithIntParameters()
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "usp_Test", ResultSetCapacity: 100);
        for (var i = 0; i < ParameterCount; i++)
            builder.AddParameter($"@Param{i}", i, System.Data.SqlDbType.Int);
        return builder.Build();
    }

    [Benchmark(Description = "Build with N varchar parameters")]
    public StoredProcedureParameters Build_WithVarcharParameters()
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "usp_Test", ResultSetCapacity: 100);
        for (var i = 0; i < ParameterCount; i++)
            builder.AddParameter($"@Name{i}", $"Value_{i}", System.Data.SqlDbType.NVarChar);
        return builder.Build();
    }

    [Benchmark(Description = "Build with mixed parameter types")]
    public StoredProcedureParameters Build_WithMixedParameters()
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "usp_Test", ResultSetCapacity: 100);
        for (var i = 0; i < ParameterCount; i++)
        {
            if (i % 3 == 0)
                builder.AddParameter($"@Int{i}", i, System.Data.SqlDbType.Int);
            else if (i % 3 == 1)
                builder.AddParameter($"@Str{i}", $"val_{i}", System.Data.SqlDbType.NVarChar);
            else
                builder.AddParameter($"@Bit{i}", i % 2 == 0, System.Data.SqlDbType.Bit);
        }
        return builder.Build();
    }
}
