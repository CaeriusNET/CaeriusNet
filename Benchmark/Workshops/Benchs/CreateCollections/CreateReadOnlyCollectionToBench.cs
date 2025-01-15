using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections;

[MemoryDiagnoser]
public class CreateReadOnlyCollectionToBench
{
	private static readonly ReadOnlyCollection<SimpleDto> Data = CreateCollectionBogusSetup.Faking100KItemsDto;

	private readonly List<SimpleDto> _data1 = Data.Take(1).ToList();
	private readonly List<SimpleDto> _data10 = Data.Take(10).ToList();
	private readonly List<SimpleDto> _data100 = Data.Take(100).ToList();
	private readonly List<SimpleDto> _data100K = Data.ToList();
	private readonly List<SimpleDto> _data10K = Data.Take(10000).ToList();
	private readonly List<SimpleDto> _data1K = Data.Take(1000).ToList();

	[Benchmark]
	public ReadOnlyCollection<SimpleDto> Set_Capacity_And_Return_1_Item_Collection_As_ReadOnlyCollection()
	{
		var list = new List<SimpleDto>(1);
		list.AddRange(_data1);
		return list.AsReadOnly();
	}

	[Benchmark]
	public ReadOnlyCollection<SimpleDto> Set_Capacity_And_Return_10_Items_Collection_As_ReadOnlyCollection()
	{
		var list = new List<SimpleDto>(10);
		list.AddRange(_data10);
		return list.AsReadOnly();
	}

	[Benchmark]
	public ReadOnlyCollection<SimpleDto> Set_Capacity_And_Return_100_Items_Collection_As_ReadOnlyCollection()
	{
		var list = new List<SimpleDto>(100);
		list.AddRange(_data100);
		return list.AsReadOnly();
	}

	[Benchmark]
	public ReadOnlyCollection<SimpleDto> Set_Capacity_And_Return_1K_Items_Collection_As_ReadOnlyCollection()
	{
		var list = new List<SimpleDto>(1000);
		list.AddRange(_data1K);
		return list.AsReadOnly();
	}

	[Benchmark]
	public ReadOnlyCollection<SimpleDto> Set_Capacity_And_Return_10000_Items_Collection_As_ReadOnlyCollection()
	{
		var list = new List<SimpleDto>(10000);
		list.AddRange(_data10K);
		return list.AsReadOnly();
	}

	[Benchmark]
	public ReadOnlyCollection<SimpleDto> Set_Capacity_And_Return_100K_Items_Collection_As_ReadOnlyCollection()
	{
		var list = new List<SimpleDto>(100000);
		list.AddRange(_data100K);
		return list.AsReadOnly();
	}
}