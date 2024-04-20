using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Factories;
using CaeriusNet.Sandbox.Models.Dtos;

namespace CaeriusNet.Sandbox.Repositories;

public interface IAdvSandboxRepository
{
    Task<IEnumerable<UsersDto>> GetUsers();
}
public sealed record AdvSandboxRepository(ICaeriusDbConnectionFactory Connection)
    : IAdvSandboxRepository
{
    public Task<IEnumerable<UsersDto>> GetUsers()
    {
        var spParameters = new StoredProcedureParametersBuilder("dbo.sp_get_users");
        return Connection.AdvQueryAsync<UsersDto>(spParameters);
    }
}