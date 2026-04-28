namespace CaeriusNet.Benchmark.Data.Simple;

/// <summary>Minimal DTO used for collection read/create benchmarks (no source generator).</summary>
public sealed class SimpleDto
{
    public SimpleDto(int id, Guid guid, string name)
    {
        Id = id;
        Guid = guid;
        Name = name;
    }

    public SimpleDto()
    {
    }

    public int Id { get; set; }
    public Guid Guid { get; set; }
    public string Name { get; set; } = null!;
}
