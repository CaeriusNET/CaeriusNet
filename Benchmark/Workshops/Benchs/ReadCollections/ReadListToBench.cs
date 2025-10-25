using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

[MemoryDiagnoser]
public class ReadListToBench
{
	private static readonly List<SimpleDto> List = ReadCollectionBogusSetup.FakingListOf10KItemsDto;

	private readonly Consumer _consumer = new();
	private readonly List<SimpleDto> _listOf100Items = List.Take(100).ToList();
	private readonly List<SimpleDto> _listOf10Items = List.Take(10).ToList();
	private readonly List<SimpleDto> _listOf10KItems = List;

	private readonly List<SimpleDto> _listOf1Item = List.Take(1).ToList();
	private readonly List<SimpleDto> _listOf1KItems = List.Take(1000).ToList();

	[Benchmark]
	public void Read_List_Of_1_Item()
	{
		int sum = _listOf1Item.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_List_Of_10_Items()
	{
		int sum = _listOf10Items.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_List_Of_100_Items()
	{
		int sum = _listOf100Items.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_List_Of_1K_Items()
	{
		int sum = _listOf1KItems.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_List_Of_10K_Items()
	{
		int sum = _listOf10KItems.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}
}