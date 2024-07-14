using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

[MemoryDiagnoser]
public class ReadAllCollectionTypesToBench
{
    private static readonly ReadOnlyCollection<SimpleDto> ReadOnlyCollection =
        ReadCollectionBogusSetup.FakingReadOnlyCollectionOf100KItemsDto;

    private static readonly List<SimpleDto> List = ReadCollectionBogusSetup.FakingListOf100KItemsDto;

    private static readonly ImmutableArray<SimpleDto> ImmutableArray =
        ReadCollectionBogusSetup.FakingImmutableArrayOf100KItemsDto;

    private static readonly IEnumerable<SimpleDto> Enumerable = ReadCollectionBogusSetup.FakingIEnumerableOf100KItemsDto;
    private IEnumerable<SimpleDto> _enumerableOf25KItems = null!;
    private ImmutableArray<SimpleDto> _immutableArrayOf25KItems;
    private List<SimpleDto> _listOf25KItems = null!;

    private ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf25KItems = null!;

    [GlobalSetup]
    public void Setup()
    {
        _readOnlyCollectionOf25KItems = ReadOnlyCollection.Take(25_000).ToList().AsReadOnly();
        _listOf25KItems = List.Take(25_000).ToList();
        _immutableArrayOf25KItems = [..ImmutableArray.Take(25_000)];
        _enumerableOf25KItems = Enumerable.Take(25_000);
    }

    [Benchmark]
    public void Read_ReadOnlyCollection()
    {
        var sum = _readOnlyCollectionOf25KItems.Sum(item => item.Id);
        _ = sum;
    }

    [Benchmark]
    public void Read_List()
    {
        var sum = _listOf25KItems.Sum(item => item.Id);
        _ = sum;
    }

    [Benchmark]
    public void Read_ImmutableArray()
    {
        var sum = _immutableArrayOf25KItems.Sum(item => item.Id);
        _ = sum;
    }

    [Benchmark]
    public void Read_Enumerable()
    {
        var sum = _enumerableOf25KItems.Sum(item => item.Id);
        _ = sum;
    }
}