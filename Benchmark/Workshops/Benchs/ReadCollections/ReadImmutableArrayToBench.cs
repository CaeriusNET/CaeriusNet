using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

[MemoryDiagnoser]
public class ReadImmutableArrayToBench
{
	private static readonly ImmutableArray<SimpleDto> ImmutableArray =
		ReadCollectionBogusSetup.FakingImmutableArrayOf10KItemsDto;

	private readonly Consumer _consumer = new();

	private readonly ImmutableArray<SimpleDto> _immutableArrayOf100Items = [..ImmutableArray.Take(100)];
	private readonly ImmutableArray<SimpleDto> _immutableArrayOf100KItems = ImmutableArray;
	private readonly ImmutableArray<SimpleDto> _immutableArrayOf10Items = [..ImmutableArray.Take(10)];
	private readonly ImmutableArray<SimpleDto> _immutableArrayOf10KItems = [..ImmutableArray.Take(1000)];
	private readonly ImmutableArray<SimpleDto> _immutableArrayOf1Item = [..ImmutableArray.Take(1)];
	private readonly ImmutableArray<SimpleDto> _immutableArrayOf1KItems = [..ImmutableArray.Take(1000)];

	[Benchmark]
	public void Read_ImmutableArray_Of_1_Item()
	{
		int sum = _immutableArrayOf1Item.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_ImmutableArray_Of_10_Items()
	{
		int sum = _immutableArrayOf10Items.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_ImmutableArray_Of_100_Items()
	{
		int sum = _immutableArrayOf100Items.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_ImmutableArray_Of_1K_Items()
	{
		int sum = _immutableArrayOf1KItems.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_ImmutableArray_Of_10K_Items()
	{
		int sum = _immutableArrayOf10KItems.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_ImmutableArray_Of_100K_Items()
	{
		int sum = _immutableArrayOf100KItems.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}
}