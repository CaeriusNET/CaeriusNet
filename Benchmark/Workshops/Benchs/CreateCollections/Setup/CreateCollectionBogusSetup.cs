using CaeriusNet.Benchmark.Data.Simple;

namespace CaeriusNet.Benchmark.Workshops.Benchs.CreateCollections.Setup;

public static class CreateCollectionBogusSetup
{
	private static readonly Random Seeding = Randomizer.Seed = new Random(31031996);

	public static readonly ReadOnlyCollection<SimpleDto> Faking10KItemsDto = new Faker<SimpleDto>()
		.StrictMode(true)
		.RuleFor(property: dto => dto.Id, setter: (faker, _) => faker.Random.Number(0, 10_000))
		.RuleFor(property: dto => dto.Guid, setter: f => f.Random.Guid())
		.RuleFor(property: dto => dto.Name, setter: f => f.Internet.UserName())
		.Generate(10_000)
		.AsReadOnly();
}