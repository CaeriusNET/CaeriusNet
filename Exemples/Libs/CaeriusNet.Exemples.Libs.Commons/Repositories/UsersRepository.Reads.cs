namespace CaeriusNet.Exemples.Libs.Commons.Repositories;

/// <summary>
///     Single-result-set read queries, with and without cache tiers.
///     These methods demonstrate the standard stored-procedure read pattern
///     and the three CaeriusNet cache layers: Frozen, InMemory and Redis.
/// </summary>
public sealed partial class UsersRepository
{
    // ─── No cache (always hits the database) ────────────────────────────────

    public async Task<IEnumerable<UserDto>> GetAllUsers(CancellationToken cancellationToken = default)
    {
        var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 25)
            .Build();

        return await dbContext.QueryAsIEnumerableAsync<UserDto>(sp, cancellationToken) ?? [];
    }

    // ─── Frozen cache (immutable snapshot, lives for the process lifetime) ──

    public async Task<IEnumerable<UserDto>> GetAllUsersWithFrozenCache(CancellationToken cancellationToken = default)
    {
        var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 25)
            .AddFrozenCache("all_users_frozen")
            .Build();

        return await dbContext.QueryAsIEnumerableAsync<UserDto>(sp, cancellationToken) ?? [];
    }

    // ─── In-memory cache (per-process, time-limited) ────────────────────────

    public async Task<IEnumerable<UserDto>> GetAllUsersWithMemoryCache(CancellationToken cancellationToken = default)
    {
        var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 25)
            .AddInMemoryCache("all_users_memory", TimeSpan.FromMinutes(1))
            .Build();

        return await dbContext.QueryAsIEnumerableAsync<UserDto>(sp, cancellationToken) ?? [];
    }

    // ─── Redis distributed cache (shared across instances) ──────────────────

    public async Task<IEnumerable<UserDto>> GetAllUsersWithRedisCache(CancellationToken cancellationToken = default)
    {
        var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 25)
            .AddRedisCache("all_users_redis", TimeSpan.FromMinutes(2))
            .Build();

        return await dbContext.QueryAsIEnumerableAsync<UserDto>(sp, cancellationToken) ?? [];
    }

    // ─── Write ───────────────────────────────────────────────────────────────

    public async Task CreateNewUser(CancellationToken cancellationToken = default)
    {
        var sp = new StoredProcedureParametersBuilder("Users", "usp_Create_User")
            .AddParameter("@UserName", $"demo-{Guid.NewGuid():N}"[..32], SqlDbType.NVarChar)
            .Build();

        await dbContext.ExecuteAsync(sp, cancellationToken);
    }
}