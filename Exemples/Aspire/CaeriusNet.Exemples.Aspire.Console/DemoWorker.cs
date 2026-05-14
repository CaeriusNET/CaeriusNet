using CaeriusNet.Exemples.Aspire.Console.DemoSections;

namespace CaeriusNet.Exemples.Aspire.Console;

/// <summary>
///     Orchestrates all CaeriusNet demo sections after the .NET generic host has fully started.
///     Executing inside a <see cref="BackgroundService" /> ensures the OTel
///     <c>TelemetryHostedService</c> has already registered its <c>ActivityListener</c> before any SQL
///     call is made, so <c>CaeriusDiagnostics.ActivitySource.HasListeners()</c> returns
///     <see langword="true" /> and every span is exported to the Aspire dashboard.
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
            System.Console.WriteLine("  • Source : CaeriusNet");
            System.Console.WriteLine("  • Spans  : SP Users.usp_*  (kind=Client, db.system=mssql)");
            System.Console.WriteLine("  • TX     : TX (kind=Internal, caerius.tx.outcome=committed|rolled-back)");
            System.Console.WriteLine(
                "  • Meter  : caerius.sp.duration / caerius.sp.executions / caerius.sp.errors / caerius.cache.lookups");
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
