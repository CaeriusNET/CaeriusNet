using CaeriusNet;

namespace CaeriusNet.Examples.Common.Models.Dtos;

[GenerateDto]
public sealed partial record OrderDto(int OrderId, int UserId, string Label, decimal Amount, DateTime CreatedAt);
