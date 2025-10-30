namespace CaeriusNet.Exemples.Libs.Commons.Repositories;

public sealed class UsersRepository(ICaeriusNetDbContext dbContext) : IUsersRepository
{
	public async Task<IEnumerable<UserDto>> GetAllUsers()
	{
		var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 25)
			.Build();

		var dbResults = await dbContext.QueryAsIEnumerableAsync<UserDto>(sp);
		return dbResults ?? [];
	}
	public async Task<IEnumerable<UserDto>> GetAllUsersWithFrozenCache()
	{
		var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 25)
			.AddFrozenCache("all_users_frozen")
			.Build();

		var dbResults = await dbContext.QueryAsIEnumerableAsync<UserDto>(sp);
		return dbResults ?? [];
	}
	public async Task<IEnumerable<UserDto>> GetAllUsersWithMemoryCache()
	{
		var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 25)
			.AddInMemoryCache("all_users_memory", TimeSpan.FromMinutes(1))
			.Build();

		var dbResults = await dbContext.QueryAsIEnumerableAsync<UserDto>(sp);
		return dbResults ?? [];
	}
	public async Task<IEnumerable<UserDto>> GetAllUsersWithRedisCache()
	{
		var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 25)
			.AddRedisCache("all_users_redis", TimeSpan.FromMinutes(2))
			.Build();

		var dbResults = await dbContext.QueryAsIEnumerableAsync<UserDto>(sp);
		return dbResults ?? [];
	}
	public async Task<IEnumerable<UserDto>> GetUsersByTvpIntGuid()
	{
		IEnumerable<UsersIntGuidTvp> usersList =
		[
			new(1, Guid.Parse("9ad8423e-f36b-1410-8b92-00ad90cc3640")),
			new(2, Guid.Parse("9cd8423e-f36b-1410-8b92-00ad90cc3640")),
			new(3, Guid.Parse("9ed8423e-f36b-1410-8b92-00ad90cc3640")),
			new(4, Guid.Parse("a0d8423e-f36b-1410-8b92-00ad90cc3640")),
			new(5, Guid.Parse("a2d8423e-f36b-1410-8b92-00ad90cc3640"))
		];

		var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_Users_From_TvpIntGuid", 5)
			.AddTvpParameter("tvp", usersList)
			.Build();

		var dbResults = await dbContext.QueryAsIEnumerableAsync<UserDto>(sp);
		return dbResults ?? [];
	}
	public async Task<IReadOnlyCollection<UserDto>> GetUsersByTvpInt()
	{
		IEnumerable<UsersIntTvp> usersList =
		[
			new(6),
			new(7),
			new(8),
			new(9),
			new(10)
		];

		var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_Users_From_TvpInt", 5)
			.AddTvpParameter("tvp", usersList)
			.Build();

		var dbResults = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp);
		return dbResults;
	}
	public async Task<ImmutableArray<UserDto>> GetUsersByTvpGuid()
	{
		var guid1 = Guid.Parse("b1d8423e-f36b-1410-8b92-00ad90cc3640");
		var guid2 = Guid.Parse("b6d8423e-f36b-1410-8b92-00ad90cc3640");
		var guid3 = Guid.Parse("bbd8423e-f36b-1410-8b92-00ad90cc3640");
		var guid4 = Guid.Parse("c0d8423e-f36b-1410-8b92-00ad90cc3640");
		var guid5 = Guid.Parse("c5d8423e-f36b-1410-8b92-00ad90cc3640");
		IEnumerable<UsersGuidTvp> usersList =
		[
			new(guid1),
			new(guid2),
			new(guid3),
			new(guid4),
			new(guid5)
		];

		var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_Users_From_TvpGuid", 5)
			.AddTvpParameter("tvp", usersList)
			.Build();

		var dbResults = await dbContext.QueryAsImmutableArrayAsync<UserDto>(sp);
		return dbResults;
	}
	public async Task CreateNewUser()
	{
		var sp = new StoredProcedureParametersBuilder("Users", "usp_Create_User")
			.Build();

		await dbContext.ExecuteAsync(sp);
	}
}