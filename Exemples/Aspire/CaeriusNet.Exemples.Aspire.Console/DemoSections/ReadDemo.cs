using CaeriusNet.Exceptions;

namespace CaeriusNet.Exemples.Aspire.Console.DemoSections;

/// <summary>
///     Demonstrates single-result-set reads with all three CaeriusNet cache tiers.
///     Run this section and inspect the Aspire Traces tab:
///     <list type="bullet">
///         <item>First call to each SP creates a <c>SP Users.usp_Get_All_Users</c> span (db hit).</item>
///         <item>Subsequent Redis/Frozen/InMemory hits emit no DB span — only <c>caerius.cache.hit=true</c>.</item>
///     </list>
/// </summary>
internal static class ReadDemo
{
    internal static async Task RunAsync(IUsersService users, CancellationToken ct)
    {
        // ── No cache — always reaches the database ───────────────────────────
        Print("All users (no cache)", await users.GetAllUsersAsync(ct));

        // ── Frozen cache — first call populates; second call is a cache hit ──
        Print("All users (frozen cache, MISS)", await users.GetAllUsersWithFrozenCacheAsync(ct));
        Print("All users (frozen cache, HIT)", await users.GetAllUsersWithFrozenCacheAsync(ct));

        // ── In-memory cache (1-minute TTL) ───────────────────────────────────
        Print("All users (in-memory cache)", await users.GetAllUsersWithMemoryCacheAsync(ct));

        // ── Redis distributed cache — miss then hit ───────────────────────────
        Print("All users (Redis cache, MISS)", await users.GetAllUsersWithRedisCacheAsync(ct));
        Print("All users (Redis cache, HIT)", await users.GetAllUsersWithRedisCacheAsync(ct));

        // ── Fallback pattern: handle a CaeriusNetSqlException gracefully ─────
        // This shows how callers should guard against transient SQL failures
        // without crashing the entire worker.  We simulate the pattern using a
        // successful call so the demo runs cleanly, but the try/catch is real.
        try
        {
            var fallbackResult = await users.GetAllUsersAsync(ct);
            Print("All users (fallback pattern, success path)", fallbackResult);
        }
        catch (CaeriusNetSqlException ex)
        {
            System.Console.WriteLine($"   ➤ [fallback] SQL error — returning empty list. {ex.Message}");
        }
    }

    private static void Print<T>(string label, IEnumerable<T> items)
    {
        System.Console.WriteLine($"   ➤ {label}");
        foreach (var item in items)
            System.Console.WriteLine($"     {item}");
    }
}