using CaeriusNet.Benchmark.Data.Simple;

namespace CaeriusNet.Benchmark.Workshops.Benchs.ListCapacity.Setup;

public static class ListCapacityBogusSetup
{
    private static readonly Random Seeding = Randomizer.Seed = new Random(31031996);

    public static readonly ReadOnlyCollection<SimpleDto> Faking50KItemsDto = new Faker<SimpleDto>()
        .StrictMode(true)
        .RuleFor(dto => dto.Id, (faker, _) => faker.Random.Number(0, 100_000))
        .RuleFor(dto => dto.Guid, f => f.Random.Guid())
        .RuleFor(dto => dto.Name, f => f.Internet.UserName())
        .Generate(100_000)
        .AsReadOnly();
}