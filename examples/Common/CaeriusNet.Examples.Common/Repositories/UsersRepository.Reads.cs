namespace CaeriusNet.Examples.Common.Repositories;

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
        var command = new StoredProcedureCommandBuilder("Users", "usp_Get_All_Users", 25)
            .Build();

        return await dbContext.QueryAsync<UserDto>(command, cancellationToken: cancellationToken);
    }

    // ─── Frozen cache (immutable snapshot, lives for the process lifetime) ──

    public async Task<IEnumerable<UserDto>> GetAllUsersWithFrozenCache(CancellationToken cancellationToken = default)
    {
        if (frozenCache.TryGet<UserDto[]>(AllUsersFrozenSnapshotKey, out var snapshot))
            return snapshot!;

        var command = new StoredProcedureCommandBuilder("Users", "usp_Get_All_Users", 25)
            .Build();

        var users = (await dbContext.QueryAsync<UserDto>(command, cancellationToken: cancellationToken))
            .ToArray();
        frozenCache.ReplaceSnapshot([new KeyValuePair<string, UserDto[]>(AllUsersFrozenSnapshotKey, users)]);
        return users;
    }

    // ─── In-memory cache (per-process, time-limited) ────────────────────────

    public async Task<IEnumerable<UserDto>> GetAllUsersWithMemoryCache(CancellationToken cancellationToken = default)
    {
        var command = new StoredProcedureCommandBuilder("Users", "usp_Get_All_Users", 25)
            .UseInMemoryCache(TimeSpan.FromMinutes(1))
            .Build();

        return await dbContext.QueryAsync<UserDto>(command, cancellationToken: cancellationToken);
    }

    // ─── Redis distributed cache (shared across instances) ──────────────────

    public async Task<IEnumerable<UserDto>> GetAllUsersWithRedisCache(CancellationToken cancellationToken = default)
    {
        var command = new StoredProcedureCommandBuilder("Users", "usp_Get_All_Users", 25)
            .UseRedisCache(TimeSpan.FromMinutes(2))
            .Build();

        return await dbContext.QueryAsync<UserDto>(command, cancellationToken: cancellationToken);
    }
}
