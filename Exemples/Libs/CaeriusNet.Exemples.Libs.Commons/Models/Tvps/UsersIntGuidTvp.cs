namespace CaeriusNet.Exemples.Libs.Commons.Models.Tvps;

[GenerateTvp(Schema = "Types", TvpName = "tvp_IntGuid")]
public sealed partial record UsersIntGuidTvp(int UserId, Guid UserGuid);