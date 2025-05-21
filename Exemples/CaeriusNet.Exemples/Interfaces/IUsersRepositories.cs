using CaeriusNet.Exemples.Models.Dtos;

namespace CaeriusNet.Exemples.Interfaces;

public interface IUsersRepositories
{
	Task<IEnumerable<ManualUsersDto>> GetAllUsersAsync();
	Task<SrcGenUsersDto> GetAllUsersInformationAsync();
}