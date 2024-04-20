using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

[MemoryDiagnoser]
public class ReadListToBench
{
    private static readonly List<SimpleDto> List = ReadCollectionBogusSetup.FakingListOf1MItemsDto;
    private List<SimpleDto> _listOf100Items = null!;
    private List<SimpleDto> _listOf100KItems = null!;
    private List<SimpleDto> _listOf10Items = null!;
    private List<SimpleDto> _listOf10KItems = null!;

    private List<SimpleDto> _listOf1Item = null!;
    private List<SimpleDto> _listOf1KItems = null!;
    private List<SimpleDto> _listOf1MItems = null!;

    [GlobalSetup]
    public void Setup()
    {
        _listOf1Item = List.Take(1).ToList();
        _listOf10Items = List.Take(10).ToList();
        _listOf100Items = List.Take(100).ToList();
        _listOf1KItems = List.Take(1000).ToList();
        _listOf10KItems = List.Take(10000).ToList();
        _listOf100KItems = List.Take(100000).ToList();
        _listOf1MItems = List;
    }

    [Benchmark]
    public void Read_List_Of_1_Item()
    {
        var sum = 0;
        foreach (var item in _listOf1Item) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_List_Of_10_Items()
    {
        var sum = 0;
        foreach (var item in _listOf10Items) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_List_Of_100_Items()
    {
        var sum = 0;
        foreach (var item in _listOf100Items) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_List_Of_1K_Items()
    {
        var sum = 0;
        foreach (var item in _listOf1KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_List_Of_10K_Items()
    {
        var sum = 0;
        foreach (var item in _listOf10KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_List_Of_100K_Items()
    {
        var sum = 0;
        foreach (var item in _listOf100KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_List_Of_1M_Items()
    {
        var sum = 0;
        foreach (var item in _listOf1MItems) sum += item.Id;

        _ = sum;
    }
}