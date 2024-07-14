using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ListCapacity.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ListCapacity;

[MemoryDiagnoser]
public class ListWithoutCapacityToBench
{
    private static readonly ReadOnlyCollection<SimpleDto> Data = ListCapacityBogusSetup.Faking50KItemsDto;

    private readonly Consumer _consumer = new();

    private readonly List<SimpleDto> _data1 = Data.Take(1).ToList();
    private readonly List<SimpleDto> _data10 = Data.Take(10).ToList();
    private readonly List<SimpleDto> _data100 = Data.Take(100).ToList();
    private readonly List<SimpleDto> _data100K = Data.ToList();
    private readonly List<SimpleDto> _data10K = Data.Take(10000).ToList();
    private readonly List<SimpleDto> _data1K = Data.Take(1000).ToList();

    [Benchmark]
    public List<SimpleDto> Set_No_Capacity_With_1_Item_To_Add()
    {
        var list = _data1.ToList();
        _consumer.Consume(list);
        return list;
    }

    [Benchmark]
    public List<SimpleDto> Set_No_Capacity_With_10_Items_To_Add()
    {
        var list = _data10.ToList();
        _consumer.Consume(list);
        return list;
    }

    [Benchmark]
    public List<SimpleDto> Set_No_Capacity_With_100_Items_To_Add()
    {
        var list = _data100.ToList();
        _consumer.Consume(list);
        return list;
    }

    [Benchmark]
    public List<SimpleDto> Set_No_Capacity_With_1K_Items_To_Add()
    {
        var list = _data1K.ToList();
        _consumer.Consume(list);
        return list;
    }

    [Benchmark]
    public List<SimpleDto> Set_No_Capacity_With_10K_Items_To_Add()
    {
        var list = _data10K.ToList();
        _consumer.Consume(list);
        return list;
    }

    [Benchmark]
    public List<SimpleDto> Set_No_Capacity_With_100K_Items_To_Add()
    {
        var list = _data100K.ToList();
        _consumer.Consume(list);
        return list;
    }
}