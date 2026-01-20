using CaeriusNet.Benchmark.Data.Simple;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ReadCollections.Setup;

public static class ReadCollectionBogusSetup
{
	private static readonly Random Seeding = Randomizer.Seed = new Random(31031996);

	public static readonly ReadOnlyCollection<SimpleDto> FakingReadOnlyCollectionOf10KItemsDto = new Faker<SimpleDto>()
		.StrictMode(true)
		.RuleFor(dto => dto.Id, (faker, _) => faker.Random.Number(0, 10_000))
		.RuleFor(dto => dto.Guid, f => f.Random.Guid())
		.RuleFor(dto => dto.Name, f => f.Internet.UserName())
		.Generate(10_000)
		.AsReadOnly();

	public static readonly IEnumerable<SimpleDto> FakingIEnumerableOf10KItemsDto = new Faker<SimpleDto>()
		.StrictMode(true)
		.RuleFor(dto => dto.Id, (faker, _) => faker.Random.Number(0, 10_000))
		.RuleFor(dto => dto.Guid, f => f.Random.Guid())
		.RuleFor(dto => dto.Name, f => f.Internet.UserName())
		.Generate(10_000)
		.AsEnumerable();

	public static readonly ImmutableArray<SimpleDto> FakingImmutableArrayOf10KItemsDto =
	[
		..new Faker<SimpleDto>()
			.StrictMode(true)
			.RuleFor(dto => dto.Id, (faker, _) => faker.Random.Number(0, 10_000))
			.RuleFor(dto => dto.Guid, f => f.Random.Guid())
			.RuleFor(dto => dto.Name, f => f.Internet.UserName())
			.Generate(10_000)
	];

	public static readonly List<SimpleDto> FakingListOf10KItemsDto = new Faker<SimpleDto>()
		.StrictMode(true)
		.RuleFor(dto => dto.Id, (faker, _) => faker.Random.Number(0, 10_000))
		.RuleFor(dto => dto.Guid, f => f.Random.Guid())
		.RuleFor(dto => dto.Name, f => f.Internet.UserName())
		.Generate(10_000);
}