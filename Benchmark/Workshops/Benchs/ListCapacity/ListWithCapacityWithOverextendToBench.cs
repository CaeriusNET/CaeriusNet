using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ListCapacity.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ListCapacity;

[MemoryDiagnoser]
public class ListWithCapacityWithOverextendToBench
{
    private static readonly ReadOnlyCollection<SimpleDto> Data = ListCapacityBogusSetup.Faking50KItemsDto;

    private List<SimpleDto> _data1 = new(1);
    private List<SimpleDto> _data10 = new(10);
    private List<SimpleDto> _data100 = new(100);
    private List<SimpleDto> _data1000 = new(1_000);
    private List<SimpleDto> _data10000 = new(10_000);
    private List<SimpleDto> _data5000 = new(5_000);
    private List<SimpleDto> _data50000 = new(50_000);

    [GlobalSetup]
    public void Setup()
    {
        _data1 = Data.Take(1).ToList();
        _data10 = Data.Take(10).ToList();
        _data100 = Data.Take(100).ToList();
        _data1000 = Data.Take(1000).ToList();
        _data5000 = Data.Take(5000).ToList();
        _data10000 = Data.Take(10000).ToList();
        _data50000 = Data.ToList();
    }

    [Benchmark]
    public List<SimpleDto> Set_Capacity_With_1_Item_But_Add_1_Item_More()
    {
        var list = new List<SimpleDto>(1);
        foreach (var item in _data1) list.Add(item);
        foreach (var item in _data10.Take(1)) list.Add(item);
        return list;
    }

    [Benchmark]
    public List<SimpleDto> Set_Capacity_With_10_Items_But_Add_10_Items_More()
    {
        var list = new List<SimpleDto>(10);
        foreach (var item in _data10) list.Add(item);
        foreach (var item in _data10) list.Add(item);
        return list;
    }

    [Benchmark]
    public List<SimpleDto> Set_Capacity_With_100_Items_But_Add_100_Items_More()
    {
        var list = new List<SimpleDto>(100);
        foreach (var item in _data100) list.Add(item);
        foreach (var item in _data100) list.Add(item);
        return list;
    }

    [Benchmark]
    public List<SimpleDto> Set_Capacity_With_1000_Items_But_Add_1000_Items_More()
    {
        var list = new List<SimpleDto>(1000);
        foreach (var item in _data1000) list.Add(item);
        foreach (var item in _data1000) list.Add(item);
        return list;
    }

    [Benchmark]
    public List<SimpleDto> Set_Capacity_With_5000_Items_But_Add_5000_Items_More()
    {
        var list = new List<SimpleDto>(5000);
        foreach (var item in _data5000) list.Add(item);
        foreach (var item in _data5000) list.Add(item);
        return list;
    }

    [Benchmark]
    public List<SimpleDto> Set_Capacity_With_10000_Items_But_Add_10000_Items_More()
    {
        var list = new List<SimpleDto>(10000);
        foreach (var item in _data10000) list.Add(item);
        foreach (var item in _data10000) list.Add(item);
        return list;
    }

    [Benchmark]
    public List<SimpleDto> Set_Capacity_With_50000_Items_But_Add_50000_Items_More()
    {
        var list = new List<SimpleDto>(50000);
        foreach (var item in _data50000) list.Add(item);
        foreach (var item in _data50000) list.Add(item);
        return list;
    }
}