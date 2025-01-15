using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.ListCapacity.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ListCapacity;

[MemoryDiagnoser]
public class ListWithLessCapacityThanNeededToBench
{
	private static readonly ReadOnlyCollection<SimpleDto> Data = ListCapacityBogusSetup.Faking50KItemsDto;

	private readonly Consumer _consumer = new();

	private readonly List<SimpleDto> _data10 = Data.Take(10).ToList();
	private readonly List<SimpleDto> _data100 = Data.Take(100).ToList();
	private readonly List<SimpleDto> _data100K = Data.ToList();
	private readonly List<SimpleDto> _data10K = Data.Take(10000).ToList();
	private readonly List<SimpleDto> _data1K = Data.Take(1000).ToList();

	[Benchmark]
	public List<SimpleDto> Set_Capacity_With_2_Items_But_Add_1_Item()
	{
		var list = new List<SimpleDto>(2);
		list.AddRange(_data10.Take(1));
		_consumer.Consume(list);
		return list;
	}

	[Benchmark]
	public List<SimpleDto> Set_Capacity_With_10_Items_But_Add_5_Item()
	{
		var list = new List<SimpleDto>(10);
		list.AddRange(_data10.Take(5));
		_consumer.Consume(list);
		return list;
	}

	[Benchmark]
	public List<SimpleDto> Set_Capacity_With_100_Items_But_Add_50_Item()
	{
		var list = new List<SimpleDto>(100);
		list.AddRange(_data100.Take(50));
		_consumer.Consume(list);
		return list;
	}

	[Benchmark]
	public List<SimpleDto> Set_Capacity_With_1K_Items_But_Add_500_Item()
	{
		var list = new List<SimpleDto>(1000);
		list.AddRange(_data1K.Take(500));
		_consumer.Consume(list);
		return list;
	}

	[Benchmark]
	public List<SimpleDto> Set_Capacity_With_10K_Items_But_Add_5K_Item()
	{
		var list = new List<SimpleDto>(10000);
		list.AddRange(_data10K.Take(5000));
		_consumer.Consume(list);
		return list;
	}

	[Benchmark]
	public List<SimpleDto> Set_Capacity_With_100K_Items_But_Add_50K_Item()
	{
		var list = new List<SimpleDto>(100000);
		list.AddRange(_data100K.Take(50000));
		_consumer.Consume(list);
		return list;
	}
}