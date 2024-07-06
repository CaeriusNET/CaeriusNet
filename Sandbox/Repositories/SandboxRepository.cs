using System.Collections.ObjectModel;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using CaeriusNet.Factories;
using CaeriusNet.Sandbox.Models.Dtos;
using CaeriusNet.Sandbox.Models.Tvps;
using CaeriusNet.Sandbox.Repositories.Interfaces;

namespace CaeriusNet.Sandbox.Repositories;

public sealed record SandboxRepository(ICaeriusDbConnectionFactory Connection) : ISandboxRepository
{
    public string GetSandboxMessage()
    {
        return "Hello from the sandbox repository!";
    }

    public async Task<IEnumerable<UsersDto>> GetUsers()
    {
        var spParameters = new StoredProcedureParametersBuilder("dbo.sp_get_users", 100);
        IEnumerable<UsersDto> users = await Connection.QueryAsync<UsersDto>(spParameters);
        return users;
    }

    public async Task CreateListOfUsers(IEnumerable<NewUsersTvp> users)
    {
        var spParameters = new StoredProcedureParametersBuilder("dbo.sp_create_users_with_tvp")
            .AddParameterAsTvp("MyTvpUsers", "dbo.tvp_newUsers", users);

        var dbResults = await Connection.ExecuteAsync(spParameters);

        Console.WriteLine($"Rows affected: {dbResults}");
    }

    public Task UpdateRandomUserAge(IEnumerable<UserAgeTvp> users)
    {
        var spParameters = new StoredProcedureParametersBuilder("dbo.sp_update_user_age")
            .AddParameterAsTvp("MyTvpUserAge", "dbo.tvp_userAge", users);

        return Connection.ExecuteScalarAsync(spParameters);
    }

    public Task<ReadOnlyCollection<UsersTestingDto>> GetUsersTesting()
    {
        var spParameters = new StoredProcedureParametersBuilder("dbo.sp_get_all_users", 38000);
        return Connection.QueryAsync<UsersTestingDto>(spParameters);
    }
}