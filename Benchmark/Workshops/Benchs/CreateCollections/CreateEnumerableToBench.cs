using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections;

[MemoryDiagnoser]
public class CreateEnumerableToBench
{
    private static readonly ReadOnlyCollection<SimpleDto> Data = CreateCollectionBogusSetup.Faking10KItemsDto;

    private readonly Consumer _consumer = new();

    private readonly List<SimpleDto> _data1 = Data.Take(1).ToList();
    private readonly List<SimpleDto> _data10 = Data.Take(10).ToList();
    private readonly List<SimpleDto> _data100 = Data.Take(100).ToList();
    private readonly List<SimpleDto> _data10K = Data.ToList();
    private readonly List<SimpleDto> _data1K = Data.Take(1000).ToList();

    [Benchmark]
    public void Set_Capacity_And_Return_1_Item_Collection_As_IEnumerable()
    {
        var list = new List<SimpleDto>(1);
        list.AddRange(_data1);
        _consumer.Consume(list.AsEnumerable());
    }

    [Benchmark]
    public void Set_Capacity_And_Return_10_Items_Collection_As_IEnumerable()
    {
        var list = new List<SimpleDto>(10);
        list.AddRange(_data10);
        _consumer.Consume(list.AsEnumerable());
    }

    [Benchmark]
    public void Set_Capacity_And_Return_100_Items_Collection_As_IEnumerable()
    {
        var list = new List<SimpleDto>(100);
        list.AddRange(_data100);
        _consumer.Consume(list.AsEnumerable());
    }

    [Benchmark]
    public void Set_Capacity_And_Return_1K_Items_Collection_As_IEnumerable()
    {
        var list = new List<SimpleDto>(1000);
        list.AddRange(_data1K);
        _consumer.Consume(list.AsEnumerable());
    }

    [Benchmark]
    public void Set_Capacity_And_Return_10K_Items_Collection_As_IEnumerable()
    {
        var list = new List<SimpleDto>(10000);
        list.AddRange(_data10K);
        _consumer.Consume(list.AsEnumerable());
    }
}