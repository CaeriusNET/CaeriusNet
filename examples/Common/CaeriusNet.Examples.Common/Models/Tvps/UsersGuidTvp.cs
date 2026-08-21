namespace CaeriusNet.Examples.Common.Models.Tvps;

[GenerateTvp(Schema = "Types", TvpName = "tvp_Guid")]
public sealed partial record UsersGuidTvp(
    [SqlServerTvpColumn("UserGuid", 0, SqlDbType.UniqueIdentifier, false)]
    Guid UserGuid);
