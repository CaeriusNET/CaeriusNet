using CaeriusNet;

namespace CaeriusNet.Examples.Common.Models.Tvps;

[GenerateTvp(Schema = "Types", TvpName = "tvp_IntGuid")]
public sealed partial record UsersIntGuidTvp(int UserId, Guid UserGuid);
