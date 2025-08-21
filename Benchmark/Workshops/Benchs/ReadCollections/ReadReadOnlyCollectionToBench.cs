using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections;

[MemoryDiagnoser]
public class ReadReadOnlyCollectionToBench
{
	private static readonly ReadOnlyCollection<SimpleDto> ReadOnlyCollection =
		ReadCollectionBogusSetup.FakingReadOnlyCollectionOf100KItemsDto;

	private readonly Consumer _consumer = new();

	private readonly ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf100Items =
		ReadOnlyCollection.Take(100).ToList().AsReadOnly();

	private readonly ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf100KItems = ReadOnlyCollection;

	private readonly ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf10Items =
		ReadOnlyCollection.Take(10).ToList().AsReadOnly();

	private readonly ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf10KItems =
		ReadOnlyCollection.Take(10000).ToList().AsReadOnly();

	private readonly ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf1Item =
		ReadOnlyCollection.Take(1).ToList().AsReadOnly();

	private readonly ReadOnlyCollection<SimpleDto> _readOnlyCollectionOf1KItems =
		ReadOnlyCollection.Take(1000).ToList().AsReadOnly();

	[Benchmark]
	public void Read_ReadOnlyCollection_Of_1_Item()
	{
		int sum = _readOnlyCollectionOf1Item.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_ReadOnlyCollection_Of_10_Items()
	{
		int sum = _readOnlyCollectionOf10Items.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_ReadOnlyCollection_Of_100_Items()
	{
		int sum = _readOnlyCollectionOf100Items.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_ReadOnlyCollection_Of_1K_Items()
	{
		int sum = _readOnlyCollectionOf1KItems.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_ReadOnlyCollection_Of_10K_Items()
	{
		int sum = _readOnlyCollectionOf10KItems.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}

	[Benchmark]
	public void Read_ReadOnlyCollection_Of_100K_Items()
	{
		int sum = _readOnlyCollectionOf100KItems.Sum(item => item.Id);
		_consumer.Consume(sum);
		_ = sum;
	}
}