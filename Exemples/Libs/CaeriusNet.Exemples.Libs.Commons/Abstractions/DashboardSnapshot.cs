namespace CaeriusNet.Exemples.Libs.Commons.Abstractions;

/// <summary>
///     Snapshot returned by <see cref="IUsersRepository.GetDashboardAsync" />: a set of users,
///     a set of orders and a per-user statistics row produced by a single multi-result-set
///     stored procedure call.
/// </summary>
public sealed record DashboardSnapshot(
    IReadOnlyCollection<UserDto> Users,
    IReadOnlyCollection<OrderDto> Orders,
    IReadOnlyCollection<UserStatsDto> Stats);
