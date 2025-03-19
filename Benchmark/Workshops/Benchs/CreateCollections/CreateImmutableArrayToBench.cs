using CaeriusNet.Benchmark.Data.Simple;
using CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections.Setup;

namespace CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections;

[MemoryDiagnoser]
public class CreateImmutableArrayToBench
{
	private static readonly ReadOnlyCollection<SimpleDto> Data = CreateCollectionBogusSetup.Faking100KItemsDto;

	private readonly List<SimpleDto> _data1 = Data.Take(1).ToList();
	private readonly List<SimpleDto> _data10 = Data.Take(10).ToList();
	private readonly List<SimpleDto> _data100 = Data.Take(100).ToList();
	private readonly List<SimpleDto> _data100K = Data.ToList();
	private readonly List<SimpleDto> _data10K = Data.Take(10000).ToList();
	private readonly List<SimpleDto> _data1K = Data.Take(1000).ToList();

	[Benchmark]
	public ImmutableArray<SimpleDto> Set_Capacity_And_Return_1_Item_Collection_As_ImmutableArray()
	{
		var list = ImmutableArray.CreateBuilder<SimpleDto>(1);
		foreach (var item in _data1) list.Add(item);
		return list.ToImmutable();
	}

	[Benchmark]
	public ImmutableArray<SimpleDto> Set_Capacity_And_Return_10_Items_Collection_As_ImmutableArray()
	{
		var list = ImmutableArray.CreateBuilder<SimpleDto>(10);
		foreach (var item in _data10) list.Add(item);
		return list.ToImmutable();
	}

	[Benchmark]
	public ImmutableArray<SimpleDto> Set_Capacity_And_Return_100_Items_Collection_As_ImmutableArray()
	{
		var list = ImmutableArray.CreateBuilder<SimpleDto>(100);
		foreach (var item in _data100) list.Add(item);
		return list.ToImmutable();
	}

	[Benchmark]
	public ImmutableArray<SimpleDto> Set_Capacity_And_Return_1K_Items_Collection_As_ImmutableArray()
	{
		var list = ImmutableArray.CreateBuilder<SimpleDto>(1000);
		foreach (var item in _data1K) list.Add(item);
		return list.ToImmutable();
	}

	[Benchmark]
	public ImmutableArray<SimpleDto> Set_Capacity_And_Return_10K_Items_Collection_As_ImmutableArray()
	{
		var list = ImmutableArray.CreateBuilder<SimpleDto>(10000);
		foreach (var item in _data10K) list.Add(item);
		return list.ToImmutable();
	}

	[Benchmark]
	public ImmutableArray<SimpleDto> Set_Capacity_And_Return_100K_Items_Collection_As_ImmutableArray()
	{
		var list = ImmutableArray.CreateBuilder<SimpleDto>(100000);
		foreach (var item in _data100K) list.Add(item);
		return list.ToImmutable();
	}
}