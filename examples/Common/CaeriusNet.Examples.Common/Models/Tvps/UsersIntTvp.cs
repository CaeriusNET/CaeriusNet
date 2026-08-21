namespace CaeriusNet.Examples.Common.Models.Tvps;

[GenerateTvp(Schema = "Types", TvpName = "tvp_Int")]
public sealed partial record UsersIntTvp(
    [SqlServerTvpColumn("UserId", 0, SqlDbType.Int, false)]
    int UserId);
