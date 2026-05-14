namespace CaeriusNet.Exemples.Libs.Commons.Abstractions;

public interface IUsersRepository
{
    // Reads ---------------------------------------------------------------
    Task<IEnumerable<UserDto>> GetAllUsers(CancellationToken cancellationToken = default);
    Task<IEnumerable<UserDto>> GetAllUsersWithFrozenCache(CancellationToken cancellationToken = default);
    Task<IEnumerable<UserDto>> GetAllUsersWithMemoryCache(CancellationToken cancellationToken = default);
    Task<IEnumerable<UserDto>> GetAllUsersWithRedisCache(CancellationToken cancellationToken = default);
    Task<IEnumerable<UserDto>> GetUsersByTvpIntGuid(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserDto>> GetUsersByTvpInt(CancellationToken cancellationToken = default);
    Task<ImmutableArray<UserDto>> GetUsersByTvpGuid(CancellationToken cancellationToken = default);

    // Writes --------------------------------------------------------------
    Task CreateNewUser(CancellationToken cancellationToken = default);

    // Multi-result sets ---------------------------------------------------
    Task<DashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyCollection<UserDto> Users, IReadOnlyCollection<OrderDto> Orders)>
        GetUsersWithOrdersByTvpAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);

    // Transactions --------------------------------------------------------
    /// <summary>
    ///     Uses the C# transaction API: creates a user, creates one of their orders and
    ///     commits. Returns the new user identifier.
    /// </summary>
    Task<int> CreateUserWithFirstOrderAsync(string userName, string firstOrderLabel, decimal amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     C#-side rollback: opens a transaction, performs writes, then rolls back without
    ///     committing — nothing is persisted.
    /// </summary>
    Task DemonstrateClientSideRollbackAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>
    ///     SQL-side rollback: invokes <c>Users.usp_Create_User_Tx_Safe</c> with
    ///     <c>@ForceFailure = 1</c>. The stored procedure rolls back inside <c>BEGIN CATCH</c>
    ///     and re-throws — the call surfaces as a <see cref="CaeriusNetSqlException" />.
    /// </summary>
    Task DemonstrateServerSideRollbackAsync(string userName, CancellationToken cancellationToken = default);
}
