namespace CaeriusNet.Exemples.Aspire.Console.DemoSections;

/// <summary>
///     Demonstrates Table-Valued Parameter (TVP) driven reads.
///     A TVP lets you pass a set of values to a stored procedure as a typed temporary table,
///     avoiding dynamic SQL and IN-list limitations.
///     <para>
///         In the Aspire Traces tab each span shows <c>caerius.tvp.used = true</c> and
///         <c>caerius.tvp.type_name</c> with the SQL Server user-defined table type name.
///     </para>
/// </summary>
internal static class TvpReadDemo
{
    internal static async Task RunAsync(IUsersService users, CancellationToken ct)
    {
        // ── Types.tvp_Int — pass a list of integer user IDs ──────────────────
        // Calls usp_Get_Users_From_TvpInt with ids {1, 2, 3, 4}.
        Print("Filter by tvp_Int  (ids 1-4)",
            await users.GetUsersByTvpIntAsync(ct));

        // ── Types.tvp_Guid — pass a list of GUID user identifiers ────────────
        // Calls usp_Get_Users_From_TvpGuid with 3 specific GUIDs.
        PrintArray("Filter by tvp_Guid (3 GUIDs)",
            await users.GetUsersByTvpGuidAsync(ct));

        // ── Types.tvp_IntGuid — pass (int, Guid) composite key pairs ─────────
        // Calls usp_Get_Users_From_TvpIntGuid — the SP joins on both columns,
        // so only rows where BOTH parts match are returned.
        Print("Filter by tvp_IntGuid (composite key pairs)",
            await users.GetUsersByTvpIntGuidAsync(ct));
    }

    private static void Print<T>(string label, IEnumerable<T> items)
    {
        System.Console.WriteLine($"   ➤ {label}");
        foreach (var item in items)
            System.Console.WriteLine($"     {item}");
    }

    private static void PrintArray<T>(string label, IEnumerable<T> items)
    {
        Print(label, items);
    }
}
