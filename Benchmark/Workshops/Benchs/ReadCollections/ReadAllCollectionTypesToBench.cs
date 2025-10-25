using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

[MemoryDiagnoser]
public class ReadAllCollectionTypesToBench
{
	private static readonly ReadOnlyCollection<SimpleDto> ReadOnlyCollection =
		ReadCollectionBogusSetup.FakingReadOnlyCollectionOf10KItemsDto;

	private static readonly List<SimpleDto> List = ReadCollectionBogusSetup.FakingListOf10KItemsDto;

	private static readonly ImmutableArray<SimpleDto> ImmutableArray =
		ReadCollectionBogusSetup.FakingImmutableArrayOf10KItemsDto;

	private static readonly IEnumerable<SimpleDto>
		Enumerable = ReadCollectionBogusSetup.FakingIEnumerableOf10KItemsDto;

	private IEnumerable<SimpleDto> _enumerableOf5KItems = null!;
	private ImmutableArray<SimpleDto> _immutableArrayOf5KItems;
	private List<SimpleDto> _listOf5KItems = null!;

	private ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf5KItems = null!;

	[GlobalSetup]
	public void Setup()
	{
		_readOnlyCollectionOf5KItems = ReadOnlyCollection.Take(5_000).ToList().AsReadOnly();
		_listOf5KItems = List.Take(5_000).ToList();
		_immutableArrayOf5KItems = [..ImmutableArray.Take(5_000)];
		_enumerableOf5KItems = Enumerable.Take(5_000);
	}

	[Benchmark]
	public void Read_ReadOnlyCollection()
	{
		int sum = _readOnlyCollectionOf5KItems.Sum(item => item.Id);
		_ = sum;
	}

	[Benchmark]
	public void Read_List()
	{
		int sum = _listOf5KItems.Sum(item => item.Id);
		_ = sum;
	}

	[Benchmark]
	public void Read_ImmutableArray()
	{
		int sum = _immutableArrayOf5KItems.Sum(item => item.Id);
		_ = sum;
	}

	[Benchmark]
	public void Read_Enumerable()
	{
		int sum = _enumerableOf5KItems.Sum(item => item.Id);
		_ = sum;
	}
}