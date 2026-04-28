namespace CaeriusNet.Exemples.Libs.Commons.Services;

/// <summary>
///     Default implementation of <see cref="IUsersService" />.
///     Delegates every operation to <see cref="IUsersRepository" />, keeping business-logic
///     concerns separated from data-access concerns.
/// </summary>
public sealed class UsersService(IUsersRepository repository) : IUsersService
{
    // ─── Reads ───────────────────────────────────────────────────────────────

    public Task<IEnumerable<UserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        return repository.GetAllUsers(cancellationToken);
    }

    public Task<IEnumerable<UserDto>> GetAllUsersWithFrozenCacheAsync(CancellationToken cancellationToken = default)
    {
        return repository.GetAllUsersWithFrozenCache(cancellationToken);
    }

    public Task<IEnumerable<UserDto>> GetAllUsersWithMemoryCacheAsync(CancellationToken cancellationToken = default)
    {
        return repository.GetAllUsersWithMemoryCache(cancellationToken);
    }

    public Task<IEnumerable<UserDto>> GetAllUsersWithRedisCacheAsync(CancellationToken cancellationToken = default)
    {
        return repository.GetAllUsersWithRedisCache(cancellationToken);
    }

    // ─── TVP-driven reads ────────────────────────────────────────────────────

    public Task<IReadOnlyCollection<UserDto>> GetUsersByTvpIntAsync(CancellationToken cancellationToken = default)
    {
        return repository.GetUsersByTvpInt(cancellationToken);
    }

    public Task<ImmutableArray<UserDto>> GetUsersByTvpGuidAsync(CancellationToken cancellationToken = default)
    {
        return repository.GetUsersByTvpGuid(cancellationToken);
    }

    public Task<IEnumerable<UserDto>> GetUsersByTvpIntGuidAsync(CancellationToken cancellationToken = default)
    {
        return repository.GetUsersByTvpIntGuid(cancellationToken);
    }

    // ─── Multi result-set reads ──────────────────────────────────────────────

    public Task<DashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        return repository.GetDashboardAsync(cancellationToken);
    }

    public Task<(IReadOnlyCollection<UserDto> Users, IReadOnlyCollection<OrderDto> Orders)>
        GetUsersWithOrdersAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        return repository.GetUsersWithOrdersByTvpAsync(userIds, cancellationToken);
    }

    // ─── Commands ────────────────────────────────────────────────────────────

    public Task<int> CreateUserWithFirstOrderAsync(string userName, string orderLabel, decimal amount,
        CancellationToken cancellationToken = default)
    {
        return repository.CreateUserWithFirstOrderAsync(userName, orderLabel, amount, cancellationToken);
    }

    public Task DemonstrateClientSideRollbackAsync(string userName, CancellationToken cancellationToken = default)
    {
        return repository.DemonstrateClientSideRollbackAsync(userName, cancellationToken);
    }

    public Task DemonstrateServerSideRollbackAsync(string userName, CancellationToken cancellationToken = default)
    {
        return repository.DemonstrateServerSideRollbackAsync(userName, cancellationToken);
    }
}
