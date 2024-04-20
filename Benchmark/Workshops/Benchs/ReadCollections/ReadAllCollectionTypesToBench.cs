using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

[MemoryDiagnoser]
public class ReadAllCollectionTypesToBench
{
    private static readonly ReadOnlyCollection<SimpleDto> ReadOnlyCollection =
        ReadCollectionBogusSetup.FakingReadOnlyCollectionOf1MItemsDto;

    private static readonly List<SimpleDto> List = ReadCollectionBogusSetup.FakingListOf1MItemsDto;

    private static readonly ImmutableArray<SimpleDto> ImmutableArray =
        ReadCollectionBogusSetup.FakingImmutableArrayOf1MItemsDto;

    private static readonly IEnumerable<SimpleDto> Enumerable = ReadCollectionBogusSetup.FakingIEnumerableOf1MItemsDto;
    private IEnumerable<SimpleDto> _enumerableOf25KItems = null!;
    private ImmutableArray<SimpleDto> _immutableArrayOf25KItems;
    private List<SimpleDto> _listOf25KItems = null!;

    private ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf25KItems = null!;

    [GlobalSetup]
    public void Setup()
    {
        _readOnlyCollectionOf25KItems = ReadOnlyCollection.Take(25_000).ToList().AsReadOnly();
        _listOf25KItems = List.Take(25_000).ToList();
        _immutableArrayOf25KItems = ImmutableArray.Take(25_000).ToImmutableArray();
        _enumerableOf25KItems = Enumerable.Take(25_000);
    }

    [Benchmark]
    public void Read_ReadOnlyCollection()
    {
        var sum = 0;
        foreach (var item in _readOnlyCollectionOf25KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_List()
    {
        var sum = 0;
        foreach (var item in _listOf25KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_ImmutableArray()
    {
        var sum = 0;
        foreach (var item in _immutableArrayOf25KItems) sum += item.Id;

        _ = sum;
    }

    [Benchmark]
    public void Read_Enumerable()
    {
        var sum = 0;
        foreach (var item in _enumerableOf25KItems) sum += item.Id;

        _ = sum;
    }
}