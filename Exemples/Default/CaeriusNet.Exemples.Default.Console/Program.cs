using CaeriusNet.Exceptions;
using CaeriusNet.Exemples.Libs.Commons.Bootstrap;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var sqlConnectionString = configuration.GetConnectionString("DefaultConnection")!;

// Bootstrap the example database (idempotent — safe to call on every startup).
Console.WriteLine("Ensuring example database schema is up to date...");
await ExampleDatabaseBootstrapper.EnsureCreatedAsync(sqlConnectionString);

var serviceCollection = new ServiceCollection()
    .AddDependenciesInjections()
    .AddLogging(loggingBuilder => loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Information));

CaeriusNetBuilder
    .Create(serviceCollection)
    .WithSqlServer(sqlConnectionString)
    .WithRedis("localhost:4567")
    .Build();

var serviceProvider = serviceCollection.BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
LoggerProvider.SetLogger(logger);

// Resolve IUsersService — the service layer wraps the repository and is the entry
// point for all business-logic interactions in this example.
var users = serviceProvider.GetRequiredService<IUsersService>();

// ---------------------------------------------------------------------------
// Each scenario below produces telemetry under the "CaeriusNet" ActivitySource
// & Meter (see Documentations/docs/documentation/aspire.md). To capture it from
// a non-Aspire host, register an OpenTelemetry exporter against:
//   • ActivitySource "CaeriusNet"
//   • Meter "CaeriusNet"
// ---------------------------------------------------------------------------

await DemoSection("1. Single result-set + caches", async () =>
{
    Print("All users (no cache)", await users.GetAllUsersAsync());
    Print("All users (frozen cache, MISS)", await users.GetAllUsersWithFrozenCacheAsync());
    Print("All users (frozen cache, HIT)", await users.GetAllUsersWithFrozenCacheAsync());
    Print("All users (in-memory cache)", await users.GetAllUsersWithMemoryCacheAsync());
});

await DemoSection("2. TVP-driven reads", async () =>
{
    Print("Filter by Types.tvp_Int", await users.GetUsersByTvpIntAsync());
    Print("Filter by Types.tvp_Guid", await users.GetUsersByTvpGuidAsync());
    Print("Filter by Types.tvp_IntGuid", await users.GetUsersByTvpIntGuidAsync());
});

await DemoSection("3. Multi result-set (caerius.resultset.multi = true)", async () =>
{
    var dashboard = await users.GetDashboardAsync();
    Console.WriteLine(
        $"   ➤ {dashboard.Users.Count} users / {dashboard.Orders.Count} orders / {dashboard.Stats.Count} stats rows");
    foreach (var stats in dashboard.Stats)
        Console.WriteLine($"     {stats.UserName,-10} {stats.OrdersCount} orders, total = {stats.TotalAmount:0.00}");
});

await DemoSection("4. TVP + multi result-set in a single call", async () =>
{
    var (selectedUsers, theirOrders) = await users.GetUsersWithOrdersAsync([1, 3, 5]);
    Console.WriteLine($"   ➤ {selectedUsers.Count} users matched, with {theirOrders.Count} orders.");
});

await DemoSection("5. Transaction — commit (caerius.tx = true)", async () =>
{
    var newUserId = await users.CreateUserWithFirstOrderAsync(
        $"default-{Guid.NewGuid():N}"[..24],
        "First default purchase",
        9.99m);
    Console.WriteLine($"   ➤ committed user #{newUserId}");
});

await DemoSection("6. Transaction — C#-side rollback", async () =>
{
    await users.DemonstrateClientSideRollbackAsync($"rollback-cs-{Guid.NewGuid():N}"[..24]);
    Console.WriteLine("   ➤ rolled back from C# — nothing persisted.");
});

await DemoSection("7. Transaction — SQL-side rollback (BEGIN CATCH)", async () =>
{
    try
    {
        await users.DemonstrateServerSideRollbackAsync($"rollback-sql-{Guid.NewGuid():N}"[..24]);
    }
    catch (CaeriusNetSqlException ex)
    {
        Console.WriteLine($"   ➤ caught CaeriusNetSqlException: {ex.InnerException?.Message}");
    }
});

Console.WriteLine();
Console.WriteLine("Examples completed. Press Enter to exit.");
Console.ReadLine();

static async Task DemoSection(string title, Func<Task> body)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 76));
    Console.WriteLine($"== {title}");
    Console.WriteLine(new string('=', 76));
    await body();
}

static void Print<T>(string label, IEnumerable<T> items)
{
    Console.WriteLine($"   ➤ {label}");
    foreach (var item in items)
        Console.WriteLine($"     {item}");
}
