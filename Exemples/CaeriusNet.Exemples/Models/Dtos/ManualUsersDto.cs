using CaeriusNet.Mappers;
using Microsoft.Data.SqlClient;

namespace CaeriusNet.Exemples.Models.Dtos;

public sealed record ManualUsersDto(int UserId, Guid UserGuid, string Username, ushort UserAge)
	: ISpMapper<ManualUsersDto>
{
	public static ManualUsersDto MapFromDataReader(SqlDataReader reader)
	{
		return new ManualUsersDto(
			reader.GetInt32(0),
			reader.GetGuid(1),
			reader.GetString(2),
			(ushort)reader.GetInt16(3)
		);
	}
}