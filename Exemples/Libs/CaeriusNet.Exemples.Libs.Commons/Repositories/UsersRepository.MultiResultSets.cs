namespace CaeriusNet.Exemples.Libs.Commons.Repositories;

/// <summary>
///     Multi-result-set queries: a single stored-procedure call that returns more than one
///     result set.  CaeriusNet maps each set to a separate DTO collection in one round-trip.
/// </summary>
public sealed partial class UsersRepository
{
    // ─── Pure multi-result-set: 3 sets in one round-trip ───────────────────

    public async Task<DashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_Dashboard", 25).Build();

        var (users, orders, stats) = await dbContext
            .QueryMultipleReadOnlyCollectionAsync<UserDto, OrderDto, UserStatsDto>(sp, cancellationToken)
            .ConfigureAwait(false);

        return new DashboardSnapshot(users, orders, stats);
    }

    // ─── TVP + multi-result-set: pass identifiers, get back 2 sets ─────────

    public async Task<(IReadOnlyCollection<UserDto> Users, IReadOnlyCollection<OrderDto> Orders)>
        GetUsersWithOrdersByTvpAsync(
            IReadOnlyCollection<int> userIds,
            CancellationToken cancellationToken = default)
    {
        var tvp = userIds.Select(id => new UsersIntTvp(id));

        var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_Users_With_Orders_By_Tvp", 25)
            .AddTvpParameter("tvp", tvp)
            .Build();

        var (users, orders) = await dbContext
            .QueryMultipleReadOnlyCollectionAsync<UserDto, OrderDto>(sp, cancellationToken)
            .ConfigureAwait(false);

        return (users, orders);
    }
}