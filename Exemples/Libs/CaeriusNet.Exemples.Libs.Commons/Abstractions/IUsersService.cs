namespace CaeriusNet.Exemples.Libs.Commons.Abstractions;

/// <summary>
///     Business-logic entry point for user-related operations.
///     The service delegates to <see cref="IUsersRepository" /> for data access and acts as
///     the boundary that domain/presentation layers depend on — keeping the repository
///     implementation hidden behind this interface.
/// </summary>
public interface IUsersService
{
    // ─── Reads ──────────────────────────────────────────────────────────────

    /// <summary>Retrieves all users without any cache.</summary>
    Task<IEnumerable<UserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Retrieves all users; results are kept in frozen (immutable) cache after the first call.</summary>
    Task<IEnumerable<UserDto>> GetAllUsersWithFrozenCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>Retrieves all users; results are kept in the in-process memory cache for 1 minute.</summary>
    Task<IEnumerable<UserDto>> GetAllUsersWithMemoryCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>Retrieves all users; results are kept in Redis for 2 minutes.</summary>
    Task<IEnumerable<UserDto>> GetAllUsersWithRedisCacheAsync(CancellationToken cancellationToken = default);

    // ─── TVP-driven reads ────────────────────────────────────────────────────

    /// <summary>Retrieves users matching a set of integer identifiers, passed as a Table-Valued Parameter.</summary>
    Task<IReadOnlyCollection<UserDto>> GetUsersByTvpIntAsync(CancellationToken cancellationToken = default);

    /// <summary>Retrieves users matching a set of GUID identifiers, passed as a Table-Valued Parameter.</summary>
    Task<ImmutableArray<UserDto>> GetUsersByTvpGuidAsync(CancellationToken cancellationToken = default);

    /// <summary>Retrieves users matching a set of (int, Guid) pairs, passed as a Table-Valued Parameter.</summary>
    Task<IEnumerable<UserDto>> GetUsersByTvpIntGuidAsync(CancellationToken cancellationToken = default);

    // ─── Multi result-set reads ──────────────────────────────────────────────

    /// <summary>
    ///     Fetches users, orders and aggregate statistics in a single round-trip by calling a stored
    ///     procedure that returns three result sets.
    /// </summary>
    Task<DashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Fetches users and their orders for the specified identifiers in a single round-trip,
    ///     using a TVP to pass the identifiers and returning two result sets.
    /// </summary>
    Task<(IReadOnlyCollection<UserDto> Users, IReadOnlyCollection<OrderDto> Orders)>
        GetUsersWithOrdersAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);

    // ─── Commands ────────────────────────────────────────────────────────────

    /// <summary>
    ///     Creates a new user and attaches a first order inside a committed transaction.
    ///     Returns the new user's identifier.
    /// </summary>
    Task<int> CreateUserWithFirstOrderAsync(string userName, string orderLabel, decimal amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Opens a transaction, writes a user, then rolls it back from the C# side —
    ///     demonstrating explicit client-controlled rollback.
    /// </summary>
    Task DemonstrateClientSideRollbackAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Calls a stored procedure that intentionally fails inside <c>BEGIN CATCH</c> and re-throws,
    ///     demonstrating SQL Server-side rollback surfaced as a <see cref="CaeriusNetSqlException" />.
    /// </summary>
    Task DemonstrateServerSideRollbackAsync(string userName, CancellationToken cancellationToken = default);
}