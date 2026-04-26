namespace CaeriusNet.Exemples.Libs.Commons.Repositories;

/// <summary>
///     TVP-driven read queries.
///     These methods pass a set of identifiers to SQL Server as a Table-Valued Parameter,
///     letting the engine filter inside the stored procedure instead of using an IN-list.
///     The three TVP types mirror the <c>Types.tvp_*</c> user-defined table types in the database.
/// </summary>
public sealed partial class UsersRepository
{
    // ─── Types.tvp_Int — filter by a set of integer user IDs ───────────────

    public async Task<IReadOnlyCollection<UserDto>> GetUsersByTvpInt(CancellationToken cancellationToken = default)
    {
        IEnumerable<UsersIntTvp> ids = [new(1), new(2), new(3), new(4)];

        var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_Users_From_TvpInt", 5)
            .AddTvpParameter("tvp", ids)
            .Build();

        return await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, cancellationToken);
    }

    // ─── Types.tvp_Guid — filter by a set of GUID user identifiers ─────────

    public async Task<ImmutableArray<UserDto>> GetUsersByTvpGuid(CancellationToken cancellationToken = default)
    {
        IEnumerable<UsersGuidTvp> guids =
        [
            new(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            new(Guid.Parse("55555555-5555-5555-5555-555555555555"))
        ];

        var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_Users_From_TvpGuid", 5)
            .AddTvpParameter("tvp", guids)
            .Build();

        return await dbContext.QueryAsImmutableArrayAsync<UserDto>(sp, cancellationToken);
    }

    // ─── Types.tvp_IntGuid — filter by a composite (int, Guid) key ─────────

    public async Task<IEnumerable<UserDto>> GetUsersByTvpIntGuid(CancellationToken cancellationToken = default)
    {
        IEnumerable<UsersIntGuidTvp> pairs =
        [
            new(1, Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new(2, Guid.Parse("22222222-2222-2222-2222-222222222222")),
            new(3, Guid.Parse("33333333-3333-3333-3333-333333333333"))
        ];

        var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_Users_From_TvpIntGuid", 5)
            .AddTvpParameter("tvp", pairs)
            .Build();

        return await dbContext.QueryAsIEnumerableAsync<UserDto>(sp, cancellationToken) ?? [];
    }
}