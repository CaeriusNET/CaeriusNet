using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections;

[MemoryDiagnoser]
public class CreateEnumerableToBench
{
    private static readonly ReadOnlyCollection<SimpleDto> Data = CreateCollectionBogusSetup.Faking100KItemsDto;

    private readonly Consumer _consumer = new();

    private readonly List<SimpleDto> _data1 = Data.Take(1).ToList();
    private readonly List<SimpleDto> _data10 = Data.Take(10).ToList();
    private readonly List<SimpleDto> _data100 = Data.Take(100).ToList();
    private readonly List<SimpleDto> _data1000 = Data.Take(1000).ToList();
    private readonly List<SimpleDto> _data10000 = Data.Take(10000).ToList();
    private readonly List<SimpleDto> _data100000 = Data.Take(100000).ToList();
    private readonly List<SimpleDto> _data1000000 = Data.ToList();

    [Benchmark]
    public void Set_Capacity_And_Return_1_Item_Collection_As_IEnumerable()
    {
        var list = new List<SimpleDto>(1);
        foreach (var item in _data1) list.Add(item);

        _consumer.Consume(list.AsEnumerable());
    }

    [Benchmark]
    public void Set_Capacity_And_Return_10_Items_Collection_As_IEnumerable()
    {
        var list = new List<SimpleDto>(10);
        foreach (var item in _data10) list.Add(item);

        _consumer.Consume(list.AsEnumerable());
    }

    [Benchmark]
    public void Set_Capacity_And_Return_100_Items_Collection_As_IEnumerable()
    {
        var list = new List<SimpleDto>(100);
        foreach (var item in _data100) list.Add(item);

        _consumer.Consume(list.AsEnumerable());
    }

    [Benchmark]
    public void Set_Capacity_And_Return_1000_Items_Collection_As_IEnumerable()
    {
        var list = new List<SimpleDto>(1000);
        foreach (var item in _data1000) list.Add(item);

        _consumer.Consume(list.AsEnumerable());
    }

    [Benchmark]
    public void Set_Capacity_And_Return_10000_Items_Collection_As_IEnumerable()
    {
        var list = new List<SimpleDto>(10000);
        foreach (var item in _data10000) list.Add(item);

        _consumer.Consume(list.AsEnumerable());
    }

    [Benchmark]
    public void Set_Capacity_And_Return_100000_Items_Collection_As_IEnumerable()
    {
        var list = new List<SimpleDto>(100000);
        foreach (var item in _data100000) list.Add(item);

        _consumer.Consume(list.AsEnumerable());
    }

    [Benchmark]
    public void Set_Capacity_And_Return_1000000_Items_Collection_As_IEnumerable()
    {
        var list = new List<SimpleDto>(1000000);
        foreach (var item in _data1000000) list.Add(item);

        _consumer.Consume(list.AsEnumerable());
    }
}