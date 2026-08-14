using CaeriusNet;

namespace CaeriusNet.Examples.Common.Models.Tvps;

[GenerateTvp(Schema = "Types", TvpName = "tvp_Int")]
public sealed partial record UsersIntTvp(int UserId);
