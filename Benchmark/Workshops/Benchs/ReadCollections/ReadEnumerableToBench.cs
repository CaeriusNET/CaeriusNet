using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

[MemoryDiagnoser]
public class ReadEnumerableToBench
{
	private static readonly IEnumerable<SimpleDto>
		Enumerable = ReadCollectionBogusSetup.FakingIEnumerableOf10KItemsDto;

	private readonly Consumer _consumer = new();

	private readonly IEnumerable<SimpleDto> _enumerableOf100Items = Enumerable.Take(100);
	private readonly IEnumerable<SimpleDto> _enumerableOf100KItems = Enumerable.Take(100000);
	private readonly IEnumerable<SimpleDto> _enumerableOf10Items = Enumerable.Take(10);
	private readonly IEnumerable<SimpleDto> _enumerableOf10KItems = Enumerable.Take(1000);
	private readonly IEnumerable<SimpleDto> _enumerableOf1Item = Enumerable.Take(1);
	private readonly IEnumerable<SimpleDto> _enumerableOf1KItems = Enumerable.Take(1000);

	[Benchmark]
	public void Read_Enumerable_Of_1_Item()
	{
		var sum = _enumerableOf1Item.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_Enumerable_Of_10_Items()
	{
		var sum = _enumerableOf10Items.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_Enumerable_Of_100_Items()
	{
		var sum = _enumerableOf100Items.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_Enumerable_Of_1K_Items()
	{
		var sum = _enumerableOf1KItems.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_Enumerable_Of_10K_Items()
	{
		var sum = _enumerableOf10KItems.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_Enumerable_Of_100K_Items()
	{
		var sum = _enumerableOf100KItems.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}
}