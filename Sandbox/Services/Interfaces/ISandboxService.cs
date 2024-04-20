using System.Collections.ObjectModel;
using CaeriusNet.Sandbox.Models.Dtos;

namespace CaeriusNet.Sandbox.Services.Interfaces;

public interface ISandboxService
{
    string GetSandboxMessage();
    Task<IEnumerable<UsersDto>> GetUsers();
    Task CreateListOfUsers();
    Task UpdateRandomUserAge(IEnumerable<UsersDto> users);

    Task<ReadOnlyCollection<UsersTestingDto>> GetUsersTesting();
}