using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections;

[MemoryDiagnoser]
public class CreateImmutableArrayToBench
{
    private static readonly ReadOnlyCollection<SimpleDto> Data = CreateCollectionBogusSetup.Faking100KItemsDto;

    private readonly List<SimpleDto> _data1 = Data.Take(1).ToList();
    private readonly List<SimpleDto> _data10 = Data.Take(10).ToList();
    private readonly List<SimpleDto> _data100 = Data.Take(100).ToList();
    private readonly List<SimpleDto> _data1000 = Data.Take(1000).ToList();
    private readonly List<SimpleDto> _data10000 = Data.Take(10000).ToList();

    [Benchmark]
    public ImmutableArray<SimpleDto> Set_Capacity_And_Return_1_Item_Collection_As_ImmutableArray()
    {
        var list = ImmutableArray.CreateBuilder<SimpleDto>(1);
        foreach (var item in _data1) list.Add(item);

        return list.ToImmutable();
    }

    [Benchmark]
    public ImmutableArray<SimpleDto> Set_Capacity_And_Return_10_Items_Collection_As_ImmutableArray()
    {
        var list = ImmutableArray.CreateBuilder<SimpleDto>(10);
        foreach (var item in _data10) list.Add(item);
        return list.ToImmutable();
    }

    [Benchmark]
    public ImmutableArray<SimpleDto> Set_Capacity_And_Return_100_Items_Collection_As_ImmutableArray()
    {
        var list = ImmutableArray.CreateBuilder<SimpleDto>(100);
        foreach (var item in _data100) list.Add(item);

        return list.ToImmutable();
    }

    [Benchmark]
    public ImmutableArray<SimpleDto> Set_Capacity_And_Return_1000_Items_Collection_As_ImmutableArray()
    {
        var list = ImmutableArray.CreateBuilder<SimpleDto>(1000);
        foreach (var item in _data1000) list.Add(item);
        return list.ToImmutable();
    }

    [Benchmark]
    public ImmutableArray<SimpleDto> Set_Capacity_And_Return_10000_Items_Collection_As_ImmutableArray()
    {
        var list = ImmutableArray.CreateBuilder<SimpleDto>(10000);
        foreach (var item in _data10000) list.Add(item);
        return list.ToImmutable();
    }
}