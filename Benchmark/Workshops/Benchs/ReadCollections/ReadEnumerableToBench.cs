using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

[MemoryDiagnoser]
public class ReadEnumerableToBench
{
    private static readonly IEnumerable<SimpleDto> Enumerable = ReadCollectionBogusSetup.FakingIEnumerableOf1MItemsDto;
    private IEnumerable<SimpleDto> _enumerableOf100Items = null!;
    private IEnumerable<SimpleDto> _enumerableOf100KItems = null!;
    private IEnumerable<SimpleDto> _enumerableOf10Items = null!;
    private IEnumerable<SimpleDto> _enumerableOf10KItems = null!;

    private IEnumerable<SimpleDto> _enumerableOf1Item = null!;
    private IEnumerable<SimpleDto> _enumerableOf1KItems = null!;
    private IEnumerable<SimpleDto> _enumerableOf1MItems = null!;

    [GlobalSetup]
    public void Setup()
    {
        _enumerableOf1Item = Enumerable.Take(1);
        _enumerableOf10Items = Enumerable.Take(10);
        _enumerableOf100Items = Enumerable.Take(100);
        _enumerableOf1KItems = Enumerable.Take(1000);
        _enumerableOf10KItems = Enumerable.Take(10000);
        _enumerableOf100KItems = Enumerable.Take(100000);
        _enumerableOf1MItems = Enumerable;
    }

    [Benchmark]
    public void Read_Enumerable_Of_1_Item()
    {
        var sum = 0;
        foreach (var item in _enumerableOf1Item) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_Enumerable_Of_10_Items()
    {
        var sum = 0;
        foreach (var item in _enumerableOf10Items) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_Enumerable_Of_100_Items()
    {
        var sum = 0;
        foreach (var item in _enumerableOf100Items) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_Enumerable_Of_1K_Items()
    {
        var sum = 0;
        foreach (var item in _enumerableOf1KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_Enumerable_Of_10K_Items()
    {
        var sum = 0;
        foreach (var item in _enumerableOf10KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_Enumerable_Of_100K_Items()
    {
        var sum = 0;
        foreach (var item in _enumerableOf100KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_Enumerable_Of_1M_Items()
    {
        var sum = 0;
        foreach (var item in _enumerableOf1MItems) sum += item.Id;

        _ = sum;
    }
}