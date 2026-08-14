using CaeriusNet.Examples.Aspire.Console.DemoSections;

namespace CaeriusNet.Examples.Aspire.Console;

/// <summary>
///     Orchestrates all CaeriusNet demo sections after the .NET generic host has fully started.
///     Executing inside a <see cref="BackgroundService" /> ensures the OTel
///     OpenTelemetry has registered its listeners before any SQL call is made, so every
///     CaeriusNet span is exported to the Aspire dashboard.
///     <para>
///         Demo sections are in the <c>DemoSections/</c> folder — one file per concern.
///     </para>
/// </summary>
internal sealed class DemoWorker(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUsersService>();

            await Section("1. Single result-set + caches",
                () => ReadDemo.RunAsync(users, stoppingToken));

            await Section("2. TVP-driven reads",
                () => TvpReadDemo.RunAsync(users, stoppingToken));

            await Section("3. Multi result-set + TVP+MultiRS",
                () => MultiResultSetDemo.RunAsync(users, stoppingToken));

            await Section("4. Transactions (commit / C# rollback / SQL rollback)",
                () => TransactionDemo.RunAsync(users, stoppingToken));

            System.Console.WriteLine();
            System.Console.WriteLine("Examples completed. Inspect spans in the Aspire dashboard:");
            System.Console.WriteLine("  • Source : CaeriusNet.SqlClient");
            System.Console.WriteLine(
                "  • Spans  : EXECUTE Users.usp_* (kind=Client, db.system.name=microsoft.sql_server)");
            System.Console.WriteLine("  • TX     : child command spans have caerius.transactional=true");
            System.Console.WriteLine(
                "  • Meter  : db.client.operation.duration / caerius.cache.lookups / caerius.cache.errors");
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    private static async Task Section(string title, Func<Task> body)
    {
        System.Console.WriteLine();
        System.Console.WriteLine(new string('=', 76));
        System.Console.WriteLine($"== {title}");
        System.Console.WriteLine(new string('=', 76));
        await body();
    }
}
