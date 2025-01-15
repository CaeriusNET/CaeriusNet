using System.Collections.ObjectModel;
using CaeriusNet.Sandbox.Models.Dtos;
using CaeriusNet.Sandbox.Models.Tvps;
using CaeriusNet.Sandbox.Repositories.Interfaces;
using CaeriusNet.Sandbox.Services.Interfaces;

namespace CaeriusNet.Sandbox.Services;

public sealed record SandboxService(ISandboxRepository SandboxRepository) : ISandboxService
{
	private readonly Random _random = new();

	public string GetSandboxMessage()
	{
		return SandboxRepository.GetSandboxMessage();
	}

	public Task<IEnumerable<UsersDto>> GetUsers()
	{
		return SandboxRepository.GetUsers();
	}

	public Task CreateListOfUsers()
	{
		const int increment = 0;

		var usersToCreate = Enumerable
			.Range(1, _random.Next(1, 1500))
			.Select(i => new NewUsersTvp($"User{i + increment}", "pass"))
			.ToList();

		return SandboxRepository.CreateListOfUsers(usersToCreate);
	}

	public Task UpdateRandomUserAge(IEnumerable<UsersDto> users)
	{
		var usersToUpdate = users
			.OrderBy(_ => _random.Next())
			.Take(50)
			.Select(u => new UserAgeTvp(u.Guid, (short)_random.Next(3, 20)))
			.ToList();

		return SandboxRepository.UpdateRandomUserAge(usersToUpdate);
	}

	public async Task<ReadOnlyCollection<UsersTestingDto>> GetUsersTesting()
	{
		return await SandboxRepository.GetUsersTesting();
	}
}