using CaeriusNet.Exceptions;

namespace CaeriusNet.Exemples.Aspire.Console.DemoSections;

/// <summary>
///     Demonstrates all three transaction scenarios offered by CaeriusNet.
///     <para>
///         In the Aspire Traces tab, each scenario produces a single <c>TX</c> parent span
///         (kind = Internal) containing child <c>SP Users.usp_*</c> spans tagged with
///         <c>caerius.tx = true</c>.  The parent span carries <c>caerius.tx.isolation_level</c>
///         and <c>caerius.tx.outcome</c> (committed / rolled-back / poisoned-auto-rollback).
///     </para>
///     Scenarios:
///     <list type="bullet">
///         <item>
///             <b>Commit</b>: two writes (create user + create order) committed atomically.
///         </item>
///         <item>
///             <b>C#-side rollback</b>: write followed by an explicit <c>RollbackAsync</c>
///             from application code — nothing is persisted.
///         </item>
///         <item>
///             <b>SQL-side rollback (BEGIN CATCH)</b>: the stored procedure wraps its work
///             in a <c>BEGIN TRY / BEGIN CATCH</c> block and re-throws on forced failure.
///             CaeriusNet surfaces the exception as <see cref="CaeriusNetSqlException" />.
///         </item>
///     </list>
/// </summary>
internal static class TransactionDemo
{
    internal static async Task RunAsync(IUsersService users, CancellationToken ct)
    {
        // ── Scenario 1: commit ───────────────────────────────────────────────
        // Opens a ReadCommitted transaction, calls usp_Create_User then
        // usp_Create_Order, then commits.  Both SP spans are children of the
        // TX span; outcome = "committed".
        var newUserId = await users.CreateUserWithFirstOrderAsync(
            $"aspire-{Guid.NewGuid():N}"[..24],
            "First Aspire purchase",
            19.99m,
            ct);
        System.Console.WriteLine($"   ➤ [commit] user #{newUserId} + first order persisted.");

        // ── Scenario 2: C#-side rollback ─────────────────────────────────────
        // Opens a transaction, writes a user, then deliberately rolls back.
        // The TX span outcome = "rolled-back"; the child SP span is still
        // recorded (caerius.tx = true) but the database row is not saved.
        await users.DemonstrateClientSideRollbackAsync(
            $"rollback-cs-{Guid.NewGuid():N}"[..24], ct);
        System.Console.WriteLine("   ➤ [C# rollback] transaction rolled back — nothing persisted.");

        // ── Scenario 3: SQL-side rollback (expected error span) ─────────────
        // usp_Create_User_Tx_Safe is called with @ForceFailure=1.  The stored
        // procedure's BEGIN CATCH rolls back and re-throws.  CaeriusNet wraps
        // the SqlException as CaeriusNetSqlException and sets the SP span to
        // ActivityStatusCode.Error.  The calling code catches it gracefully.
        try
        {
            await users.DemonstrateServerSideRollbackAsync(
                $"rollback-sql-{Guid.NewGuid():N}"[..24], ct);
        }
        catch (CaeriusNetSqlException ex)
        {
            System.Console.WriteLine(
                $"   ➤ [SQL rollback] caught CaeriusNetSqlException (expected): " +
                $"{ex.InnerException?.Message}");
        }
    }
}
