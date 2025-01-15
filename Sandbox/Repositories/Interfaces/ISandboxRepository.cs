using System.Collections.ObjectModel;
using CaeriusNet.Sandbox.Models.Dtos;
using CaeriusNet.Sandbox.Models.Tvps;

namespace CaeriusNet.Sandbox.Repositories.Interfaces;

public interface ISandboxRepository
{
	string GetSandboxMessage();
	Task<IEnumerable<UsersDto>> GetUsers();
	Task CreateListOfUsers(IEnumerable<NewUsersTvp> users);
	Task UpdateRandomUserAge(IEnumerable<UserAgeTvp> users);
	Task<ReadOnlyCollection<UsersTestingDto>> GetUsersTesting();
}