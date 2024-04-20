using CaeriusNet.Mappers;
using Microsoft.Data.SqlClient;

namespace CaeriusNet.Sandbox.Models.Dtos;

public sealed record UsersTestingDto(Guid Guid, string Username, string Password)
    : ISpMapper<UsersTestingDto>
{
    public static UsersTestingDto MapFromReader(SqlDataReader reader)
    {
        return new UsersTestingDto(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2)
        );
    }
}