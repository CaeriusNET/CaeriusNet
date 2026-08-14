using CaeriusNet;

namespace CaeriusNet.Examples.Common.Models.Dtos;

[GenerateDto]
public sealed partial record UserDto(int UserId, Guid UserGuid);
