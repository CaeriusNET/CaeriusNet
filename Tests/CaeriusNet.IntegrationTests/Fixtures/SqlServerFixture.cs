using System.Text;

namespace CaeriusNet.IntegrationTests.Fixtures;

/// <summary>
///     Boots a single SQL Server 2022 container for the whole xunit collection, provisions the
///     <c>caerius_tests</c> database and schema, and exposes a configured
///     <see cref="ICaeriusNetDbContext" /> backed by <see cref="Microsoft.Data.SqlClient.SqlConnection" />.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string TestDatabaseName = "caerius_tests";

    // Image is pinned to 2022 LTS so test results stay deterministic across runners.
    // `WithReuse(true)` keeps the container warm between local `dotnet test` runs when the
    // host has `testcontainers.reuse.enable=true` in `~/.testcontainers.properties` (the
    // devcontainer wires this up automatically). On CI the env var is absent, so each job
    // starts with a fresh container — no behaviour change there.
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Caerius!Test_2026")
        .WithReuse(true)
        .Build();

    private IServiceProvider? _serviceProvider;
    private string? _testConnectionString;

    /// <summary>
    ///     Direct connection string, useful for ADO.NET probes that bypass CaeriusNet (assertions, cleanup).
    /// </summary>
    public string ConnectionString =>
        _testConnectionString ?? throw new InvalidOperationException("Fixture not initialised.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);

        var masterConnectionString = _container.GetConnectionString();
        var builder = new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = TestDatabaseName,
            TrustServerCertificate = true
        };
        _testConnectionString = builder.ConnectionString;

        await CreateDatabaseAsync(masterConnectionString).ConfigureAwait(false);
        await ExecuteSchemaScriptAsync().ConfigureAwait(false);

        var services = new ServiceCollection();
        CaeriusNetBuilder.Create(services)
            .WithSqlServer(_testConnectionString)
            .Build();
        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable async)
            await async.DisposeAsync().ConfigureAwait(false);
        else if (_serviceProvider is IDisposable sync)
            sync.Dispose();

        await _container.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     A fresh DI scope that exposes <see cref="ICaeriusNetDbContext" /> registered as scoped.
    ///     Tests <b>must</b> dispose the returned <see cref="IServiceScope" /> (use <c>using</c>).
    /// </summary>
    public IServiceScope CreateScope()
    {
        if (_serviceProvider is null)
            throw new InvalidOperationException("Fixture not initialised. InitializeAsync did not run.");
        return _serviceProvider.CreateScope();
    }

    /// <summary>
    ///     Truncates the <c>Widgets</c> table and reseeds identities so each test sees a clean slate
    ///     while keeping the schema and stored procedures intact (cheap compared to dropping the DB).
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              DELETE FROM dbo.Widgets;
                              DBCC CHECKIDENT('dbo.Widgets', RESEED, 0) WITH NO_INFOMSGS;
                              """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CreateDatabaseAsync(string masterConnectionString)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $$"""
                                IF DB_ID(N'{{TestDatabaseName}}') IS NULL
                                    CREATE DATABASE [{{TestDatabaseName}}];
                                """;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task ExecuteSchemaScriptAsync()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Sql", "schema.sql");
        var script = await File.ReadAllTextAsync(schemaPath).ConfigureAwait(false);

        // Microsoft.Data.SqlClient does not parse GO; split on it manually (case-insensitive, line-anchored).
        var batches = SplitOnGo(script);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        foreach (var batch in batches)
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    private static IEnumerable<string> SplitOnGo(string script)
    {
        using var reader = new StringReader(script);
        var current = new StringBuilder();
        string? line;
        while ((line = reader.ReadLine()) is not null)
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                yield return current.ToString();
                current.Clear();
            }
            else
            {
                current.AppendLine(line);
            }

        if (current.Length > 0) yield return current.ToString();
    }
}

/// <summary>
///     xunit collection that materialises a single <see cref="SqlServerFixture" /> for all integration
///     tests. Container boot is the dominant cost, so sharing it is essential.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
