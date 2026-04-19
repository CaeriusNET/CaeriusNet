namespace CaeriusNet.Exemples.Libs.Commons.Abstractions;

public interface IUsersRepository
{
    Task<IEnumerable<UserDto>> GetAllUsers();
    Task<IEnumerable<UserDto>> GetAllUsersWithFrozenCache();
    Task<IEnumerable<UserDto>> GetAllUsersWithMemoryCache();
    Task<IEnumerable<UserDto>> GetAllUsersWithRedisCache();
    Task<IEnumerable<UserDto>> GetUsersByTvpIntGuid();
    Task<IReadOnlyCollection<UserDto>> GetUsersByTvpInt();
    Task<ImmutableArray<UserDto>> GetUsersByTvpGuid();
    Task CreateNewUser();
}