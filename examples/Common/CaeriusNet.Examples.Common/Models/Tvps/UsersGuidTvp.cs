using CaeriusNet;

namespace CaeriusNet.Examples.Common.Models.Tvps;

[GenerateTvp(Schema = "Types", TvpName = "tvp_Guid")]
public sealed partial record UsersGuidTvp(Guid UserGuid);
