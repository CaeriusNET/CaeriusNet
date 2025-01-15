using CaeriusNet.Mappers;
using Microsoft.Data.SqlClient;

namespace CaeriusNet.Sandbox.Models.Dtos;

public sealed record UsersDto(Guid Guid, string User, string Pass, short Age) : ISpMapper<UsersDto>
{
	public static UsersDto MapFromDataReader(SqlDataReader record)
	{
		return new UsersDto(
			record.GetGuid(0),
			record.GetString(1),
			record.GetString(2),
			(short)record.GetSqlByte(3)
		);
	}
}