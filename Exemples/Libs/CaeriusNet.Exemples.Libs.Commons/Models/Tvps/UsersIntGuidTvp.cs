namespace CaeriusNet.Exemples.Libs.Commons.Models.Tvps;

[GenerateTvp(Schema = "Types", TvpName = "tvp_IntGuid")]
public sealed record UsersIntGuidTvp(int UserId, Guid UserGuid);