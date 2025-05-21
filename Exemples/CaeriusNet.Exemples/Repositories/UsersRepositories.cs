using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Exemples.Interfaces;
using CaeriusNet.Exemples.Models.Dtos;
using CaeriusNet.Factories;

namespace CaeriusNet.Exemples.Repositories;

public sealed record UsersRepositories(ICaeriusDbContext DbContext) : IUsersRepositories
{
	public async Task<IEnumerable<ManualUsersDto>> GetAllUsersAsync()
	{
		var storedProcedureParameters = new StoredProcedureParametersBuilder("Users.usp_get_all_users").Build();

		var dbResult = await DbContext.QueryAsIEnumerableAsync<ManualUsersDto>(storedProcedureParameters);
		return dbResult;
	}

	public async Task<SrcGenUsersDto> GetAllUsersInformationAsync()
	{
		var spParam = new StoredProcedureParametersBuilder("Users.usp_get_all_users_information").Build();

		var dbResult = await DbContext.FirstQueryAsync<SrcGenUsersDto>(spParam);
		return dbResult!;
	}
}