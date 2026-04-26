namespace CaeriusNet.Exemples.Libs.Commons.Repositories;

/// <summary>
///     Transactional write operations.
///     Each method demonstrates a distinct transaction scenario:
///     <list type="bullet">
///         <item>
///             <see cref="CreateUserWithFirstOrderAsync" /> — two writes committed atomically
///             via the CaeriusNet transaction API.
///         </item>
///         <item>
///             <see cref="DemonstrateClientSideRollbackAsync" /> — write followed by a
///             deliberate C#-side rollback (nothing is persisted).
///         </item>
///         <item>
///             <see cref="DemonstrateServerSideRollbackAsync" /> — stored procedure that wraps
///             its own <c>BEGIN TRY / BEGIN CATCH</c> block and re-throws, surfaced as a
///             <see cref="CaeriusNetSqlException" />.
///         </item>
///     </list>
///     Every SP call made inside an <c>ICaeriusNetTransaction</c> scope is automatically tagged
///     with <c>caerius.tx = true</c> and appears as a child span of the parent <c>TX</c> activity
///     in the Aspire / OTel dashboard.
/// </summary>
public sealed partial class UsersRepository
{
    // ─── Scenario 1: two writes, committed atomically ───────────────────────

    public async Task<int> CreateUserWithFirstOrderAsync(
        string userName,
        string firstOrderLabel,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await dbContext
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        var createUser = new StoredProcedureParametersBuilder("Users", "usp_Create_User")
            .AddParameter("@UserName", userName, SqlDbType.NVarChar)
            .Build();

        var newUserId = await tx
            .ExecuteScalarAsync<int>(createUser, cancellationToken)
            .ConfigureAwait(false);

        if (newUserId == 0)
            throw new InvalidOperationException(
                $"usp_Create_User returned invalid user identifier ({newUserId}); cannot create the associated order.");

        var createOrder = new StoredProcedureParametersBuilder("Users", "usp_Create_Order")
            .AddParameter("@UserId", newUserId, SqlDbType.Int)
            .AddParameter("@Label", firstOrderLabel, SqlDbType.NVarChar)
            .AddParameter("@Amount", amount, SqlDbType.Decimal)
            .Build();

        await tx.ExecuteScalarAsync<int>(createOrder, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        return newUserId;
    }

    // ─── Scenario 2: C#-side rollback ───────────────────────────────────────

    public async Task DemonstrateClientSideRollbackAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await dbContext
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        var createUser = new StoredProcedureParametersBuilder("Users", "usp_Create_User")
            .AddParameter("@UserName", userName, SqlDbType.NVarChar)
            .Build();

        // Write is performed but we deliberately decide not to keep it.
        await tx.ExecuteScalarAsync<int>(createUser, cancellationToken).ConfigureAwait(false);

        // Explicit rollback from the application layer — nothing is persisted.
        // The parent TX span records outcome = "rolled-back".
        await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
    }

    // ─── Scenario 3: SQL-side rollback (BEGIN TRY / BEGIN CATCH) ────────────

    public async Task DemonstrateServerSideRollbackAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        var sp = new StoredProcedureParametersBuilder("Users", "usp_Create_User_Tx_Safe")
            .AddParameter("@UserName", userName, SqlDbType.NVarChar)
            .AddParameter("@ForceFailure", true, SqlDbType.Bit)
            .Build();

        // The stored procedure's BEGIN CATCH rolls back internally and re-throws.
        // CaeriusNet wraps the SqlException as CaeriusNetSqlException, sets
        // ActivityStatusCode.Error on the span and attaches the exception event.
        await dbContext.ExecuteAsync(sp, cancellationToken).ConfigureAwait(false);
    }
}