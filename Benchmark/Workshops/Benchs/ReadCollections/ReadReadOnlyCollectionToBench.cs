using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

[MemoryDiagnoser]
public class ReadReadOnlyCollectionToBench
{
    private static readonly ReadOnlyCollection<SimpleDto> ReadOnlyCollection =
        ReadCollectionBogusSetup.FakingReadOnlyCollectionOf1MItemsDto;

    private ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf100Items = null!;
    private ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf100KItems = null!;
    private ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf10Items = null!;
    private ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf10KItems = null!;

    private ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf1Item = null!;
    private ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf1KItems = null!;
    private ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf1MItems = null!;

    [GlobalSetup]
    public void Setup()
    {
        _readOnlyCollectionOf1Item = ReadOnlyCollection.Take(1).ToList().AsReadOnly();
        _readOnlyCollectionOf10Items = ReadOnlyCollection.Take(10).ToList().AsReadOnly();
        _readOnlyCollectionOf100Items = ReadOnlyCollection.Take(100).ToList().AsReadOnly();
        _readOnlyCollectionOf1KItems = ReadOnlyCollection.Take(1000).ToList().AsReadOnly();
        _readOnlyCollectionOf10KItems = ReadOnlyCollection.Take(10000).ToList().AsReadOnly();
        _readOnlyCollectionOf100KItems = ReadOnlyCollection.Take(100000).ToList().AsReadOnly();
        _readOnlyCollectionOf1MItems = ReadOnlyCollection;
    }

    [Benchmark]
    public void Read_ReadOnlyCollection_Of_1_Item()
    {
        var sum = 0;
        foreach (var item in _readOnlyCollectionOf1Item) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_ReadOnlyCollection_Of_10_Items()
    {
        var sum = 0;
        foreach (var item in _readOnlyCollectionOf10Items) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_ReadOnlyCollection_Of_100_Items()
    {
        var sum = 0;
        foreach (var item in _readOnlyCollectionOf100Items) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_ReadOnlyCollection_Of_1K_Items()
    {
        var sum = 0;
        foreach (var item in _readOnlyCollectionOf1KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_ReadOnlyCollection_Of_10K_Items()
    {
        var sum = 0;
        foreach (var item in _readOnlyCollectionOf10KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_ReadOnlyCollection_Of_100K_Items()
    {
        var sum = 0;
        foreach (var item in _readOnlyCollectionOf100KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_ReadOnlyCollection_Of_1M_Items()
    {
        var sum = 0;
        foreach (var item in _readOnlyCollectionOf1MItems) sum += item.Id;

        _ = sum;
    }
}