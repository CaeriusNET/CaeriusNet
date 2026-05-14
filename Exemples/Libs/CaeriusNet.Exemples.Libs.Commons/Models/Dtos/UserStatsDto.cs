namespace CaeriusNet.Exemples.Libs.Commons.Models.Dtos;

[GenerateDto]
public sealed partial record UserStatsDto(int UserId, string UserName, int OrdersCount, decimal TotalAmount);