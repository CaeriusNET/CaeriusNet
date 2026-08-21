namespace CaeriusNet.Examples.Aspire.Console.DemoSections;

/// <summary>
///     Demonstrates multi-result-set reads: a single stored-procedure call that returns
///     more than one result set, mapped to separate DTO collections in one round-trip.
///     <para>
///         In the Aspire Traces tab each invocation is one <c>EXECUTE</c> client span;
///         CaeriusNet validates the exact requested result-set count before returning data.
///     </para>
/// </summary>
internal static class MultiResultSetDemo
{
    internal static async Task RunAsync(IUsersService users, CancellationToken ct)
    {
        // ── Pure multi-result-set (3 sets, 1 round-trip) ─────────────────────
        // usp_Get_Dashboard returns:
        //   Set #1 — all users
        //   Set #2 — all orders
        //   Set #3 — per-user totals (aggregate statistics)
        var dashboard = await users.GetDashboardAsync(ct);
        System.Console.WriteLine(
            $"   ➤ Dashboard: {dashboard.Users.Count} users / " +
            $"{dashboard.Orders.Count} orders / {dashboard.Stats.Count} stats rows");

        foreach (var stats in dashboard.Stats)
            System.Console.WriteLine(
                $"     {stats.UserName,-10} {stats.OrdersCount} orders, " +
                $"total = {stats.TotalAmount:0.00}");

        // ── TVP + multi-result-set (2 sets, filtered by TVP ids) ─────────────
        // usp_Get_Users_With_Orders_By_Tvp receives ids {1, 3, 5} as a TVP and
        // returns:
        //   Set #1 — matching users
        //   Set #2 — their orders
        // One stored-procedure round-trip with two explicitly materialized result sets.
        var (selectedUsers, theirOrders) = await users.GetUsersWithOrdersAsync([1, 3, 5], ct);
        System.Console.WriteLine(
            $"   ➤ TVP+MultiRS: {selectedUsers.Count} users matched, " +
            $"{theirOrders.Count} orders returned.");
    }
}
