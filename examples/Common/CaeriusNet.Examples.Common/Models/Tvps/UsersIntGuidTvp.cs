namespace CaeriusNet.Examples.Common.Models.Tvps;

[GenerateTvp(Schema = "Types", TvpName = "tvp_IntGuid")]
public sealed partial record UsersIntGuidTvp(
    [SqlServerTvpColumn("UserId", 0, SqlDbType.Int, false)]
    int UserId,
    [SqlServerTvpColumn("UserGuid", 1, SqlDbType.UniqueIdentifier, false)]
    Guid UserGuid);
