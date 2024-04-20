using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

[MemoryDiagnoser]
public class ReadImmutableArrayToBench
{
    private static readonly ImmutableArray<SimpleDto> ImmutableArray =
        ReadCollectionBogusSetup.FakingImmutableArrayOf1MItemsDto;

    private ImmutableArray<SimpleDto> _immutableArrayOf100Items;
    private ImmutableArray<SimpleDto> _immutableArrayOf100KItems;
    private ImmutableArray<SimpleDto> _immutableArrayOf10Items;
    private ImmutableArray<SimpleDto> _immutableArrayOf10KItems;

    private ImmutableArray<SimpleDto> _immutableArrayOf1Item;
    private ImmutableArray<SimpleDto> _immutableArrayOf1KItems;
    private ImmutableArray<SimpleDto> _immutableArrayOf1MItems;

    [GlobalSetup]
    public void Setup()
    {
        _immutableArrayOf1Item = ImmutableArray.Take(1).ToImmutableArray();
        _immutableArrayOf10Items = ImmutableArray.Take(10).ToImmutableArray();
        _immutableArrayOf100Items = ImmutableArray.Take(100).ToImmutableArray();
        _immutableArrayOf1KItems = ImmutableArray.Take(1000).ToImmutableArray();
        _immutableArrayOf10KItems = ImmutableArray.Take(10000).ToImmutableArray();
        _immutableArrayOf100KItems = ImmutableArray.Take(100000).ToImmutableArray();
        _immutableArrayOf1MItems = ImmutableArray;
    }

    [Benchmark]
    public void Read_ImmutableArray_Of_1_Item()
    {
        var sum = 0;
        foreach (var item in _immutableArrayOf1Item) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_ImmutableArray_Of_10_Items()
    {
        var sum = 0;
        foreach (var item in _immutableArrayOf10Items) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_ImmutableArray_Of_100_Items()
    {
        var sum = 0;
        foreach (var item in _immutableArrayOf100Items) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_ImmutableArray_Of_1K_Items()
    {
        var sum = 0;
        foreach (var item in _immutableArrayOf1KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_ImmutableArray_Of_10K_Items()
    {
        var sum = 0;
        foreach (var item in _immutableArrayOf10KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_ImmutableArray_Of_100K_Items()
    {
        var sum = 0;
        foreach (var item in _immutableArrayOf100KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_ImmutableArray_Of_1M_Items()
    {
        var sum = 0;
        foreach (var item in _immutableArrayOf1MItems) sum += item.Id;

        _ = sum;
    }
}