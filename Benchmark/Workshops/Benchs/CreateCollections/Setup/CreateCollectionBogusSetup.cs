using CaeriusNet.Benchmark.Data.Simple;

namespace CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections.Setup;

public static class CreateCollectionBogusSetup
{
	private static readonly Random Seeding = Randomizer.Seed = new Random(31031996);

	public static readonly ReadOnlyCollection<SimpleDto> Faking10KItemsDto = new Faker<SimpleDto>()
		.StrictMode(true)
		.RuleFor(dto => dto.Id, (faker, _) => faker.Random.Number(0, 10_000))
		.RuleFor(dto => dto.Guid, f => f.Random.Guid())
		.RuleFor(dto => dto.Name, f => f.Internet.UserName())
		.Generate(10_000)
		.AsReadOnly();
}