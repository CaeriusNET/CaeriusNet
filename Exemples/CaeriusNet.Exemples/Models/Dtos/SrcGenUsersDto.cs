using CaeriusNet.Attributes;

namespace CaeriusNet.Exemples.Models.Dtos;

[GenerateDto]
public sealed partial record SrcGenUsersDto(int UserId, Guid UserGuid, string Username);