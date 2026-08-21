using System.Collections;
using System.Data.SqlTypes;
using System.Runtime.CompilerServices;
using CaeriusNet.IntegrationTests.Infrastructure;
using CaeriusNet.Mappers;

namespace CaeriusNet.IntegrationTests.SqlServer;

/// <summary>
///     Acceptance matrix for the stored-procedure-only runtime. The test deliberately provisions its own
///     compact schema so it exercises the public DI and command APIs on both supported SQL Server images.
/// </summary>
public sealed class SqlServerAcceptanceMatrixTests
{
    [Theory]
    [MemberData(nameof(ContainerTestGate.SqlServerImages), MemberType = typeof(ContainerTestGate))]
    public async Task SqlServerAcceptanceMatrixPassesOnEachSupportedImage(string image)
    {
        ContainerTestGate.RequireEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var container = new MsSqlBuilder(image)
            .WithPassword("Caerius!Tests_2026")
            .Build();
        await container.StartAsync(cancellationToken);

        var connectionString = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            TrustServerCertificate = true
        }.ConnectionString;
        await ProvisionAsync(connectionString, cancellationToken);

        var services = new ServiceCollection();
        services.AddCaeriusNet(options => options.UseSqlServer(connectionString));
        services.AddSingleton<RecordingRemoteCache>();
        services.AddSingleton<ICaeriusRemoteCacheManager>(static provider =>
            provider.GetRequiredService<RecordingRemoteCache>());

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var remoteCache = scope.ServiceProvider.GetRequiredService<RecordingRemoteCache>();

        await AssertCardinalityAndScalarContractsAsync(database, cancellationToken);
        await AssertOutputContractsAsync(database, cancellationToken);
        await AssertSqlDecimalAndRowlessFrameContractsAsync(database, cancellationToken);
        await AssertNoResultContractsAsync(database, cancellationToken);
        await AssertTvpContractsAsync(database, cancellationToken);
        await AssertStreamingContractsAsync(database, cancellationToken);
        await AssertAllMultiResultArityAndCollectionContractsAsync(database, cancellationToken);
        await AssertVariableResultSetPoliciesAsync(database, cancellationToken);
        await AssertMultiResultCachePolicyIsolationAsync(database, cancellationToken);
        await AssertTransactionsAsync(database, remoteCache, cancellationToken);
    }

    private static async Task AssertCardinalityAndScalarContractsAsync(
        ICaeriusNetDbContext database,
        CancellationToken cancellationToken)
    {
        var none = CardinalityCommand(0);
        var one = CardinalityCommand(1);
        var several = CardinalityCommand(2);

        Assert.Null(await database.QueryFirstOrDefaultAsync<Widget>(none, cancellationToken));
        Assert.Null(await database.QuerySingleOrDefaultAsync<Widget>(none, cancellationToken));
        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
        {
            _ = await database.QueryFirstAsync<Widget>(none, cancellationToken);
        });
        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
        {
            _ = await database.QuerySingleAsync<Widget>(none, cancellationToken);
        });

        Assert.Equal(1, (await database.QueryFirstAsync<Widget>(one, cancellationToken)).Id);
        Assert.Equal(1, (await database.QuerySingleAsync<Widget>(one, cancellationToken)).Id);
        Assert.Equal(1, (await database.QueryFirstAsync<Widget>(several, cancellationToken)).Id);
        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
        {
            _ = await database.QuerySingleAsync<Widget>(several, cancellationToken);
        });

        Assert.Null(await database.ExecuteScalarOrDefaultAsync<int?>(ScalarCommand(0), cancellationToken));
        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
        {
            _ = await database.ExecuteScalarAsync<int>(ScalarCommand(0), cancellationToken);
        });
        Assert.Null(await database.ExecuteScalarOrDefaultAsync<int?>(ScalarCommand(1), cancellationToken));
        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
        {
            _ = await database.ExecuteScalarAsync<int>(ScalarCommand(1), cancellationToken);
        });
        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
        {
            _ = await database.ExecuteScalarAsync<int>(ScalarCommand(2), cancellationToken);
        });
        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
        {
            _ = await database.ExecuteScalarOrDefaultAsync<int?>(ScalarCommand(4), cancellationToken);
        });
        Assert.Equal(73, await database.ExecuteScalarAsync<int>(ScalarCommand(3), cancellationToken));

        var mapping = await Assert.ThrowsAsync<CaeriusMappingException>(async () =>
        {
            _ = await database.QuerySingleAsync<Widget>(
                new StoredProcedureCommandBuilder("dbo", "caerius_test_bad_widget").Build(), cancellationToken);
        });
        Assert.Equal(0, mapping.ResultSetIndex);
        Assert.Equal(0, mapping.RowIndex);
    }

    private static async Task AssertOutputContractsAsync(ICaeriusNetDbContext database,
        CancellationToken cancellationToken)
    {
        var queryCommand = OutputCommand("caerius_test_outputs", 4, 8);
        var queryResult =
            await database.QueryImmutableArrayWithOutputsAsync<Widget>(queryCommand,
                cancellationToken: cancellationToken);

        Assert.Equal(4, Assert.Single(queryResult.Data).Id);
        Assert.Equal(12, queryResult.Output.Get<int>("@inOut"));
        Assert.Equal(8, queryResult.Output.Get<int>("@out"));
        Assert.Equal(93, queryResult.Output.Get<int>("@returnValue"));

        var noDataResult = await database.ExecuteWithOutputsAsync(
            OutputCommand("caerius_test_output_no_data", 5, 7), cancellationToken);
        Assert.Equal(12, noDataResult.Output.Get<int>("@inOut"));
        Assert.Equal(10, noDataResult.Output.Get<int>("@out"));
        Assert.Equal(94, noDataResult.Output.Get<int>("@returnValue"));
    }

    private static async Task AssertSqlDecimalAndRowlessFrameContractsAsync(
        ICaeriusNetDbContext database,
        CancellationToken cancellationToken)
    {
        var inputOutput = SqlDecimal.Parse("9876543210987654321098765434.0987654321");
        var command = new StoredProcedureCommandBuilder("dbo", "caerius_test_sql_decimal_no_count_off")
            .AddReturnValue()
            .AddInputOutputParameter("@inputOutput", inputOutput, SqlDbType.Decimal, precision: 38, scale: 10)
            .AddOutputParameter("@output", SqlDbType.Decimal, precision: 38, scale: 10)
            .Build();

        var result = await database.QueryImmutableArrayWithOutputsAsync<SqlDecimalRow>(
            command,
            cancellationToken: cancellationToken);

        const string Expected = "1234567890123456789012345678.1234567890";
        Assert.Equal(Expected, Assert.Single(result.Data).Value.ToString());
        Assert.Equal(Expected, result.Output.Get<SqlDecimal>("@output").ToString());
        Assert.Equal(inputOutput.ToString(), result.Output.Get<SqlDecimal>("@inputOutput").ToString());
        Assert.Equal(96, result.Output.Get<int>("@returnValue"));
    }

    private static async Task AssertNoResultContractsAsync(
        ICaeriusNetDbContext database,
        CancellationToken cancellationToken)
    {
        var emptyResult = new StoredProcedureCommandBuilder("dbo", "caerius_test_unexpected_result")
            .AddParameter("@mode", 0, SqlDbType.Int)
            .Build();
        var populatedResult = new StoredProcedureCommandBuilder("dbo", "caerius_test_unexpected_result")
            .AddParameter("@mode", 1, SqlDbType.Int)
            .Build();
        var outputResult = new StoredProcedureCommandBuilder("dbo", "caerius_test_output_with_result")
            .AddOutputParameter("@out", SqlDbType.Int)
            .Build();

        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
            await database.ExecuteAsync(emptyResult, cancellationToken));
        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
            await database.ExecuteAsync(populatedResult, cancellationToken));
        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
            await database.ExecuteWithOutputsAsync(outputResult, cancellationToken));
    }

    private static async Task AssertTvpContractsAsync(ICaeriusNetDbContext database,
        CancellationToken cancellationToken)
    {
        foreach (var expectedCount in new[] { 0, 1, 1_000 })
        {
            var rows = CreateRows(expectedCount).ToArray();
            var command = new StoredProcedureCommandBuilder("dbo", "caerius_test_count_ints")
                .AddTvpParameter("@values", rows)
                .Build();

            Assert.Equal(expectedCount, await database.ExecuteScalarAsync<int>(command, cancellationToken));
        }

        // This source deliberately has no count and cannot be enumerated a second time. It exercises the
        // one-row TVP look-ahead needed by SqlClient to represent a lazy empty source as null, while proving
        // that a large source is neither restarted nor enumerated twice.
        var oneShotRows = new OneShotRows(100_000);
        var largeLazyCommand = new StoredProcedureCommandBuilder("dbo", "caerius_test_count_ints")
            .AddTvpParameter("@values", _ => oneShotRows)
            .Build();
        Assert.Equal(100_000, await database.ExecuteScalarAsync<int>(largeLazyCommand, cancellationToken));
        Assert.Equal(1, oneShotRows.EnumerationCount);
        Assert.Equal(100_000, oneShotRows.RowsYielded);
    }

    private static async Task AssertStreamingContractsAsync(
        ICaeriusNetDbContext database,
        CancellationToken cancellationToken)
    {
        var complete = new List<Widget>();
        await foreach (var row in database.StreamAsync<Widget>(StreamCommand(3), cancellationToken))
            complete.Add(row);
        Assert.Equal([1, 2, 3], complete.Select(static row => row.Id));
        Assert.Equal(0, await database.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken));

        var first = 0;
        await foreach (var row in database.StreamAsync<Widget>(StreamCommand(3), cancellationToken))
        {
            first = row.Id;
            break;
        }

        Assert.Equal(1, first);
        // A non-transactional break owns and releases a short-lived lease, so the next command is unaffected.
        Assert.Equal(0, await database.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken));

        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150)))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in database.StreamAsync<Widget>(
                                   StreamCommand(1, true), cancellation.Token))
                {
                }
            });
        }

        Assert.Equal(0, await database.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken));

        var mapping = await Assert.ThrowsAsync<CaeriusMappingException>(async () =>
        {
            await foreach (var _ in database.StreamAsync<Widget>(
                               StreamCommand(4, badAt: 3), cancellationToken))
            {
            }
        });
        Assert.Equal(0, mapping.ResultSetIndex);
        Assert.Equal(2, mapping.RowIndex);
        Assert.Equal(0, await database.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken));

        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
        {
            await foreach (var _ in database.StreamAsync<Widget>(
                               StreamCommand(2, additionalSet: true), cancellationToken))
            {
            }
        });
        Assert.Equal(0, await database.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken));

        await using (var transaction = await database.BeginTransactionAsync(cancellationToken: cancellationToken))
        {
            var transactionalRows = new List<Widget>();
            await foreach (var row in transaction.StreamAsync<Widget>(StreamCommand(3), cancellationToken))
                transactionalRows.Add(row);
            Assert.Equal([1, 2, 3], transactionalRows.Select(static row => row.Id));
            Assert.Equal(0, await transaction.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var transaction = await database.BeginTransactionAsync(cancellationToken: cancellationToken))
        {
            await foreach (var _ in transaction.StreamAsync<Widget>(StreamCommand(3), cancellationToken))
                break;
            Assert.False(transaction.IsActive);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                _ = await transaction.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await transaction.CommitAsync(cancellationToken));
            await transaction.RollbackAsync(CancellationToken.None);
        }

        await using (var transaction = await database.BeginTransactionAsync(cancellationToken: cancellationToken))
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in transaction.StreamAsync<Widget>(
                                   StreamCommand(1, true), cancellation.Token))
                {
                }
            });
            Assert.False(transaction.IsActive);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                _ = await transaction.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken);
            });
            await transaction.RollbackAsync(CancellationToken.None);
        }

        await AssertTransactionalStreamFailurePoisonsAsync(
            database,
            StreamCommand(4, badAt: 3),
            typeof(CaeriusMappingException),
            cancellationToken);
        await AssertTransactionalStreamFailurePoisonsAsync(
            database,
            StreamCommand(2, additionalSet: true),
            typeof(CaeriusContractException),
            cancellationToken);
    }

    private static async Task AssertTransactionalStreamFailurePoisonsAsync(
        ICaeriusNetDbContext database,
        StoredProcedureCommand command,
        Type expectedExceptionType,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.BeginTransactionAsync(cancellationToken: cancellationToken);
        var failure = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in transaction.StreamAsync<Widget>(command, cancellationToken))
            {
            }
        });
        Assert.NotNull(failure);
        Assert.Equal(expectedExceptionType, failure.GetType());
        Assert.False(transaction.IsActive);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = await transaction.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken);
        });
        await transaction.RollbackAsync(CancellationToken.None);
    }

    private static async Task AssertAllMultiResultArityAndCollectionContractsAsync(
        ICaeriusNetDbContext database,
        CancellationToken cancellationToken)
    {
        // Each generated overload is called directly. This is intentionally not reflection-based: a consumer
        // compiles every arity (2..10) and each of the three promised materialized collection families.
        var enumerable2 = await database.QueryMultipleAsync<Widget, Widget>(
            MultiCommand(2), 1, 1, cancellationToken: cancellationToken);
        AssertAllMultiResultSets(enumerable2);
        var readOnly2 =
            await database.QueryMultipleReadOnlyCollectionAsync<Widget, Widget>(MultiCommand(2),
                cancellationToken: cancellationToken);
        AssertAllMultiResultSets(readOnly2);
        var immutable2 =
            await database.QueryMultipleImmutableArrayAsync<Widget, Widget>(MultiCommand(2),
                cancellationToken: cancellationToken);
        AssertAllMultiResultSets(immutable2);

        var enumerable3 =
            await database.QueryMultipleAsync<Widget, Widget, Widget>(MultiCommand(3),
                cancellationToken: cancellationToken);
        AssertAllMultiResultSets(enumerable3);
        var readOnly3 =
            await database.QueryMultipleReadOnlyCollectionAsync<Widget, Widget, Widget>(MultiCommand(3),
                cancellationToken: cancellationToken);
        AssertAllMultiResultSets(readOnly3);
        var immutable3 =
            await database.QueryMultipleImmutableArrayAsync<Widget, Widget, Widget>(MultiCommand(3),
                cancellationToken: cancellationToken);
        AssertAllMultiResultSets(immutable3);

        var enumerable4 =
            await database.QueryMultipleAsync<Widget, Widget, Widget, Widget>(MultiCommand(4),
                cancellationToken: cancellationToken);
        AssertAllMultiResultSets(enumerable4);
        var readOnly4 =
            await database.QueryMultipleReadOnlyCollectionAsync<Widget, Widget, Widget, Widget>(MultiCommand(4),
                cancellationToken: cancellationToken);
        AssertAllMultiResultSets(readOnly4);
        var immutable4 =
            await database.QueryMultipleImmutableArrayAsync<Widget, Widget, Widget, Widget>(MultiCommand(4),
                cancellationToken: cancellationToken);
        AssertAllMultiResultSets(immutable4);

        var enumerable5 =
            await database.QueryMultipleAsync<Widget, Widget, Widget, Widget, Widget>(MultiCommand(5),
                cancellationToken: cancellationToken);
        AssertAllMultiResultSets(enumerable5);
        var readOnly5 =
            await database.QueryMultipleReadOnlyCollectionAsync<Widget, Widget, Widget, Widget, Widget>(MultiCommand(5),
                cancellationToken: cancellationToken);
        AssertAllMultiResultSets(readOnly5);
        var immutable5 =
            await database.QueryMultipleImmutableArrayAsync<Widget, Widget, Widget, Widget, Widget>(MultiCommand(5),
                cancellationToken: cancellationToken);
        AssertAllMultiResultSets(immutable5);

        var enumerable6 =
            await database.QueryMultipleAsync<Widget, Widget, Widget, Widget, Widget, Widget>(MultiCommand(6),
                cancellationToken: cancellationToken);
        AssertAllMultiResultSets(enumerable6);
        var readOnly6 =
            await database.QueryMultipleReadOnlyCollectionAsync<Widget, Widget, Widget, Widget, Widget, Widget>(
                MultiCommand(6), cancellationToken: cancellationToken);
        AssertAllMultiResultSets(readOnly6);
        var immutable6 =
            await database.QueryMultipleImmutableArrayAsync<Widget, Widget, Widget, Widget, Widget, Widget>(
                MultiCommand(6), cancellationToken: cancellationToken);
        AssertAllMultiResultSets(immutable6);

        var enumerable7 =
            await database.QueryMultipleAsync<Widget, Widget, Widget, Widget, Widget, Widget, Widget>(MultiCommand(7),
                cancellationToken: cancellationToken);
        AssertAllMultiResultSets(enumerable7);
        var readOnly7 =
            await database.QueryMultipleReadOnlyCollectionAsync<Widget, Widget, Widget, Widget, Widget, Widget, Widget>(
                MultiCommand(7), cancellationToken: cancellationToken);
        AssertAllMultiResultSets(readOnly7);
        var immutable7 =
            await database.QueryMultipleImmutableArrayAsync<Widget, Widget, Widget, Widget, Widget, Widget, Widget>(
                MultiCommand(7), cancellationToken: cancellationToken);
        AssertAllMultiResultSets(immutable7);

        var enumerable8 =
            await database.QueryMultipleAsync<Widget, Widget, Widget, Widget, Widget, Widget, Widget, Widget>(
                MultiCommand(8), cancellationToken: cancellationToken);
        AssertAllMultiResultSets(enumerable8);
        var readOnly8 =
            await database
                .QueryMultipleReadOnlyCollectionAsync<Widget, Widget, Widget, Widget, Widget, Widget, Widget, Widget>(
                    MultiCommand(8), cancellationToken: cancellationToken);
        AssertAllMultiResultSets(readOnly8);
        var immutable8 =
            await database
                .QueryMultipleImmutableArrayAsync<Widget, Widget, Widget, Widget, Widget, Widget, Widget, Widget>(
                    MultiCommand(8), cancellationToken: cancellationToken);
        AssertAllMultiResultSets(immutable8);

        var enumerable9 =
            await database.QueryMultipleAsync<Widget, Widget, Widget, Widget, Widget, Widget, Widget, Widget, Widget>(
                MultiCommand(9), cancellationToken: cancellationToken);
        AssertAllMultiResultSets(enumerable9);
        var readOnly9 =
            await database
                .QueryMultipleReadOnlyCollectionAsync<Widget, Widget, Widget, Widget, Widget, Widget, Widget, Widget,
                    Widget>(MultiCommand(9), cancellationToken: cancellationToken);
        AssertAllMultiResultSets(readOnly9);
        var immutable9 =
            await database
                .QueryMultipleImmutableArrayAsync<Widget, Widget, Widget, Widget, Widget, Widget, Widget, Widget,
                    Widget>(MultiCommand(9), cancellationToken: cancellationToken);
        AssertAllMultiResultSets(immutable9);

        var enumerable10 =
            await database
                .QueryMultipleAsync<Widget, Widget, Widget, Widget, Widget, Widget, Widget, Widget, Widget, Widget>(
                    MultiCommand(10), cancellationToken: cancellationToken);
        AssertAllMultiResultSets(enumerable10);
        var readOnly10 =
            await database
                .QueryMultipleReadOnlyCollectionAsync<Widget, Widget, Widget, Widget, Widget, Widget, Widget, Widget,
                    Widget, Widget>(MultiCommand(10), cancellationToken: cancellationToken);
        AssertAllMultiResultSets(readOnly10);
        var immutable10 =
            await database
                .QueryMultipleImmutableArrayAsync<Widget, Widget, Widget, Widget, Widget, Widget, Widget, Widget, Widget
                    , Widget>(MultiCommand(10), cancellationToken: cancellationToken);
        AssertAllMultiResultSets(immutable10);
    }

    private static void AssertAllMultiResultSets(object multiResult, int presentSetCount = int.MaxValue)
    {
        var sets = Assert.IsAssignableFrom<ITuple>(multiResult);
        for (var index = 0; index < sets.Length; index++)
        {
            var rows = Assert.IsAssignableFrom<IEnumerable<Widget>>(sets[index]);
            if (index < presentSetCount)
                Assert.Equal(index + 1, Assert.Single(rows).Id);
            else
                Assert.Empty(rows);
        }
    }

    private static async Task AssertVariableResultSetPoliciesAsync(
        ICaeriusNetDbContext database,
        CancellationToken cancellationToken)
    {
        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
        {
            _ = await database.QueryMultipleImmutableArrayAsync<Widget, Widget>(
                MultiCommand(1), cancellationToken: cancellationToken);
        });
        var missingAllowed = await database.QueryMultipleImmutableArrayAsync<Widget, Widget>(
            MultiCommand(1), countPolicy: CaeriusResultSetCountPolicy.AllowMissing,
            cancellationToken: cancellationToken);
        AssertAllMultiResultSets(missingAllowed, 1);
        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
            await database.QueryMultipleImmutableArrayAsync<Widget, Widget>(
                MultiCommand(1), countPolicy: CaeriusResultSetCountPolicy.AllowAdditional,
                cancellationToken: cancellationToken));
        var missingOrAdditional = await database.QueryMultipleImmutableArrayAsync<Widget, Widget>(
            MultiCommand(1), countPolicy: CaeriusResultSetCountPolicy.AllowMissingOrAdditional,
            cancellationToken: cancellationToken);
        AssertAllMultiResultSets(missingOrAdditional, 1);

        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
        {
            _ = await database.QueryMultipleImmutableArrayAsync<Widget, Widget>(
                MultiCommand(3), cancellationToken: cancellationToken);
        });
        var additionalAllowed = await database.QueryMultipleImmutableArrayAsync<Widget, Widget>(
            MultiCommand(3), countPolicy: CaeriusResultSetCountPolicy.AllowAdditional,
            cancellationToken: cancellationToken);
        AssertAllMultiResultSets(additionalAllowed, 2);
        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
            await database.QueryMultipleImmutableArrayAsync<Widget, Widget>(
                MultiCommand(3), countPolicy: CaeriusResultSetCountPolicy.AllowMissing,
                cancellationToken: cancellationToken));
        var additionalOrMissing = await database.QueryMultipleImmutableArrayAsync<Widget, Widget>(
            MultiCommand(3), countPolicy: CaeriusResultSetCountPolicy.AllowMissingOrAdditional,
            cancellationToken: cancellationToken);
        AssertAllMultiResultSets(additionalOrMissing, 2);
    }

    private static async Task AssertMultiResultCachePolicyIsolationAsync(
        ICaeriusNetDbContext database,
        CancellationToken cancellationToken)
    {
        // A result-set-count policy is part of the execution contract, not merely a reader preference. These
        // pairs use the same command and result types, so a shared cache shape would skip the second SQL call.
        var partial = CachedPolicyMultiCommand(1);
        var permissivePartial = await database.QueryMultipleImmutableArrayAsync<Widget, Widget>(
            partial,
            countPolicy: CaeriusResultSetCountPolicy.AllowMissing,
            cancellationToken: cancellationToken);
        AssertAllMultiResultSets(permissivePartial, 1);
        Assert.Equal(1, await database.ExecuteScalarAsync<int>(CachePolicyCounterCommand(), cancellationToken));

        // The exact contract must miss the permissive partial entry and validate SQL again.
        await Assert.ThrowsAsync<CaeriusContractException>(async () =>
            await database.QueryMultipleImmutableArrayAsync<Widget, Widget>(
                partial,
                countPolicy: CaeriusResultSetCountPolicy.Exactly,
                cancellationToken: cancellationToken));
        Assert.Equal(2, await database.ExecuteScalarAsync<int>(CachePolicyCounterCommand(), cancellationToken));

        var exact = CachedPolicyMultiCommand(2);
        var exactValue = await database.QueryMultipleImmutableArrayAsync<Widget, Widget>(
            exact,
            countPolicy: CaeriusResultSetCountPolicy.Exactly,
            cancellationToken: cancellationToken);
        AssertAllMultiResultSets(exactValue, 2);
        Assert.Equal(3, await database.ExecuteScalarAsync<int>(CachePolicyCounterCommand(), cancellationToken));

        // And conversely, a permissive read cannot reuse the successful exact entry even when SQL presently
        // returns the same two sets: the counter proves it executed and populated a separate cache key.
        var permissiveFull = await database.QueryMultipleImmutableArrayAsync<Widget, Widget>(
            exact,
            countPolicy: CaeriusResultSetCountPolicy.AllowMissing,
            cancellationToken: cancellationToken);
        AssertAllMultiResultSets(permissiveFull, 2);
        Assert.Equal(4, await database.ExecuteScalarAsync<int>(CachePolicyCounterCommand(), cancellationToken));
    }

    private static async Task AssertTransactionsAsync(
        ICaeriusNetDbContext database,
        RecordingRemoteCache remoteCache,
        CancellationToken cancellationToken)
    {
        var write = new StoredProcedureCommandBuilder("dbo", "caerius_test_increment_counter")
            .AddParameter("@id", 1, SqlDbType.Int)
            .Invalidates("test:counter:1")
            .Build();

        await using (var transaction = await database.BeginTransactionAsync(cancellationToken: cancellationToken))
        {
            await transaction.ExecuteAsync(write, cancellationToken);
            Assert.Empty(remoteCache.RemovedKeys);
            await transaction.CommitAsync(cancellationToken);
        }

        Assert.Contains("test:counter:1", remoteCache.RemovedKeys);
        Assert.Equal(1, await database.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken));

        // A token already cancelled before terminal ownership is taken leaves the scope active and does not
        // make an uncommitted write visible or invalidate the cache.
        remoteCache.Clear();
        await using (var transaction = await database.BeginTransactionAsync(cancellationToken: cancellationToken))
        {
            await transaction.ExecuteAsync(write, cancellationToken);
            using var cancelledBeforeCommit = new CancellationTokenSource();
            cancelledBeforeCommit.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await transaction.CommitAsync(cancelledBeforeCommit.Token));
            Assert.True(transaction.IsActive);
            Assert.Empty(remoteCache.RemovedKeys);
            await transaction.RollbackAsync(CancellationToken.None);
        }

        Assert.Equal(1, await database.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken));

        // Cancellation after COMMIT begins must not turn a durable SQL change into a cache-invalidation
        // miss. The cache double cancels the original token immediately before inspecting the supplied one.
        remoteCache.Clear();
        using (var cancelledAfterCommitBegins = new CancellationTokenSource())
        {
            remoteCache.CancelAndRequireNonCancelableRemoval(cancelledAfterCommitBegins);
            try
            {
                await using var transaction =
                    await database.BeginTransactionAsync(cancellationToken: cancellationToken);
                await transaction.ExecuteAsync(write, cancellationToken);
                await transaction.CommitAsync(cancelledAfterCommitBegins.Token);
            }
            finally
            {
                remoteCache.ClearCancellationProbe();
            }

            Assert.True(cancelledAfterCommitBegins.IsCancellationRequested);
        }

        Assert.Contains("test:counter:1", remoteCache.RemovedKeys);
        Assert.Equal(2, await database.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken));

        remoteCache.Clear();
        await using (var transaction = await database.BeginTransactionAsync(cancellationToken: cancellationToken))
        {
            await transaction.ExecuteAsync(write, cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
        }

        Assert.Empty(remoteCache.RemovedKeys);
        Assert.Equal(2, await database.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken));

        // Rollback follows the same pre-check rule, but its deferred invalidations are always discarded.
        remoteCache.Clear();
        await using (var transaction = await database.BeginTransactionAsync(cancellationToken: cancellationToken))
        {
            await transaction.ExecuteAsync(write, cancellationToken);
            using var cancelledBeforeRollback = new CancellationTokenSource();
            cancelledBeforeRollback.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await transaction.RollbackAsync(cancelledBeforeRollback.Token));
            Assert.True(transaction.IsActive);
            await transaction.RollbackAsync(CancellationToken.None);
        }

        Assert.Empty(remoteCache.RemovedKeys);
        Assert.Equal(2, await database.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken));

        // Disposal owns its rollback without a caller token and must likewise discard deferred invalidations.
        remoteCache.Clear();
        await using (var transaction = await database.BeginTransactionAsync(cancellationToken: cancellationToken))
        {
            await transaction.ExecuteAsync(write, cancellationToken);
        }

        Assert.Empty(remoteCache.RemovedKeys);
        Assert.Equal(2, await database.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken));

        await using (var transaction = await database.BeginTransactionAsync(cancellationToken: cancellationToken))
        {
            var first = transaction.ExecuteScalarAsync<int>(
                    new StoredProcedureCommandBuilder("dbo", "caerius_test_wait_scalar").Build(), cancellationToken)
                .AsTask();
            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                _ = await transaction.ExecuteScalarAsync<int>(CounterCommand(), cancellationToken);
            });
            Assert.Equal(1, await first);
            await transaction.RollbackAsync(cancellationToken);
        }

        await using (var transaction = await database.BeginTransactionAsync(cancellationToken: cancellationToken))
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                _ = await transaction.ExecuteScalarAsync<int>(
                    new StoredProcedureCommandBuilder("dbo", "caerius_test_wait_scalar").Build(),
                    cancellation.Token);
            });
            Assert.False(transaction.IsActive);
            await transaction.RollbackAsync(CancellationToken.None);
        }
    }

    private static StoredProcedureCommand CardinalityCommand(int mode)
    {
        return new StoredProcedureCommandBuilder("dbo", "caerius_test_cardinality")
            .AddParameter("@mode", mode, SqlDbType.Int)
            .Build();
    }

    private static StoredProcedureCommand ScalarCommand(int mode)
    {
        return new StoredProcedureCommandBuilder("dbo", "caerius_test_scalar")
            .AddParameter("@mode", mode, SqlDbType.Int)
            .Build();
    }

    private static StoredProcedureCommand MultiCommand(int sets)
    {
        return new StoredProcedureCommandBuilder("dbo", "caerius_test_multi")
            .AddParameter("@sets", sets, SqlDbType.Int)
            .WithResultSetCapacities(Enumerable.Repeat(1, sets).ToArray())
            .Build();
    }

    private static StoredProcedureCommand CachedPolicyMultiCommand(int mode)
    {
        return new StoredProcedureCommandBuilder("dbo", "caerius_test_multi_cache_policy")
            .AddParameter("@mode", mode, SqlDbType.Int)
            .WithResultSetCapacities(1, 1)
            .UseInMemoryCache(TimeSpan.FromMinutes(1))
            .Build();
    }

    private static StoredProcedureCommand CachePolicyCounterCommand()
    {
        return new StoredProcedureCommandBuilder("dbo", "caerius_test_read_cache_policy_counter").Build();
    }

    private static StoredProcedureCommand CounterCommand()
    {
        return new StoredProcedureCommandBuilder("dbo", "caerius_test_read_counter").Build();
    }

    private static StoredProcedureCommand StreamCommand(
        int rowCount,
        bool waitBeforeFirst = false,
        int badAt = 0,
        bool additionalSet = false)
    {
        return new StoredProcedureCommandBuilder("dbo", "caerius_test_stream_widgets")
            .AddParameter("@rowCount", rowCount, SqlDbType.Int)
            .AddParameter("@waitBeforeFirst", waitBeforeFirst, SqlDbType.Bit)
            .AddParameter("@badAt", badAt, SqlDbType.Int)
            .AddParameter("@additionalSet", additionalSet, SqlDbType.Bit)
            .Build();
    }

    private static StoredProcedureCommand OutputCommand(string procedure, int input, int inputOutput)
    {
        return new StoredProcedureCommandBuilder("dbo", procedure)
            .AddReturnValue()
            .AddParameter("@input", input, SqlDbType.Int)
            .AddInputOutputParameter("@inOut", inputOutput, SqlDbType.Int)
            .AddOutputParameter("@out", SqlDbType.Int)
            .Build();
    }

    private static IEnumerable<IntRow> CreateRows(int count)
    {
        for (var value = 1; value <= count; value++)
            yield return new IntRow(value);
    }

    private static async Task ProvisionAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
                                       IF OBJECT_ID(N'dbo.CaeriusTestCounters', N'U') IS NULL
                                           CREATE TABLE dbo.CaeriusTestCounters (Id int NOT NULL PRIMARY KEY, Value int NOT NULL);
                                       DELETE FROM dbo.CaeriusTestCounters;
                                       INSERT dbo.CaeriusTestCounters (Id, Value) VALUES (1, 0), (2, 0);
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       IF TYPE_ID(N'dbo.CaeriusTestIntList') IS NULL
                                           EXEC(N'CREATE TYPE dbo.CaeriusTestIntList AS TABLE (Value int NOT NULL)');
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_cardinality
                                           @mode int
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           IF @mode = 0
                                               SELECT CAST(0 AS int) AS Id, CAST(N'' AS nvarchar(80)) AS Name WHERE 1 = 0;
                                           ELSE IF @mode = 1
                                               SELECT 1 AS Id, N'one' AS Name;
                                           ELSE
                                               SELECT 1 AS Id, N'first' AS Name UNION ALL SELECT 2, N'second';
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_scalar
                                           @mode int
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           IF @mode = 0
                                               SELECT CAST(1 AS int) AS Value WHERE 1 = 0;
                                           ELSE IF @mode = 1
                                               SELECT CAST(NULL AS int) AS Value;
                                           ELSE IF @mode = 2
                                               SELECT N'not-an-int' AS Value;
                                           ELSE IF @mode = 4
                                               SELECT CAST(1 AS int) AS FirstValue, CAST(2 AS int) AS SecondValue WHERE 1 = 0;
                                           ELSE
                                               SELECT 73 AS Value;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_bad_widget
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           SELECT CAST(NULL AS int) AS Id, N'bad' AS Name;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_count_ints
                                           @values dbo.CaeriusTestIntList READONLY
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           SELECT COUNT_BIG(*) AS Value FROM @values;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_stream_widgets
                                           @rowCount int,
                                           @waitBeforeFirst bit,
                                           @badAt int,
                                           @additionalSet bit
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           IF @waitBeforeFirst = 1 WAITFOR DELAY '00:00:03';
                                           ;WITH Numbers AS
                                           (
                                               SELECT TOP (@rowCount)
                                                   CONVERT(int, ROW_NUMBER() OVER (ORDER BY (SELECT NULL))) AS Value
                                               FROM sys.all_objects AS firstObject
                                               CROSS JOIN sys.all_objects AS secondObject
                                           )
                                           SELECT
                                               CASE WHEN Value = @badAt THEN CAST(NULL AS int) ELSE Value END AS Id,
                                               CONCAT(N'stream-', Value) AS Name
                                           FROM Numbers
                                           ORDER BY Value;
                                           IF @additionalSet = 1 SELECT 999 AS Id, N'additional' AS Name;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_outputs
                                           @input int,
                                           @inOut int OUTPUT,
                                           @out int OUTPUT
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           SET @inOut = @inOut + @input;
                                           SET @out = @input * 2;
                                           SELECT @input AS Id, N'output' AS Name;
                                           RETURN 93;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_output_no_data
                                           @input int,
                                           @inOut int OUTPUT,
                                           @out int OUTPUT
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           SET @inOut = @inOut + @input;
                                           SET @out = @input * 2;
                                           RETURN 94;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_sql_decimal_no_count_off
                                           @inputOutput decimal(38,10) OUTPUT,
                                           @output decimal(38,10) OUTPUT
                                       AS
                                       BEGIN
                                           SET NOCOUNT OFF;
                                           UPDATE dbo.CaeriusTestCounters SET Value = Value WHERE Id = 1;
                                           SET @inputOutput = @inputOutput;
                                           SET @output = CAST(1234567890123456789012345678.1234567890 AS decimal(38,10));
                                           SELECT @output AS Value;
                                           UPDATE dbo.CaeriusTestCounters SET Value = Value WHERE Id = 1;
                                           RETURN 96;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_unexpected_result
                                           @mode int
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           IF @mode = 0
                                               SELECT CAST(1 AS int) AS Value WHERE 1 = 0;
                                           ELSE
                                               SELECT 1 AS Value;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_output_with_result
                                           @out int OUTPUT
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           SET @out = 99;
                                           SELECT 1 AS Value;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_multi
                                           @sets int
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           IF @sets >= 1 SELECT 1 AS Id, N'set-1' AS Name;
                                           IF @sets >= 2 SELECT 2 AS Id, N'set-2' AS Name;
                                           IF @sets >= 3 SELECT 3 AS Id, N'set-3' AS Name;
                                           IF @sets >= 4 SELECT 4 AS Id, N'set-4' AS Name;
                                           IF @sets >= 5 SELECT 5 AS Id, N'set-5' AS Name;
                                           IF @sets >= 6 SELECT 6 AS Id, N'set-6' AS Name;
                                           IF @sets >= 7 SELECT 7 AS Id, N'set-7' AS Name;
                                           IF @sets >= 8 SELECT 8 AS Id, N'set-8' AS Name;
                                           IF @sets >= 9 SELECT 9 AS Id, N'set-9' AS Name;
                                           IF @sets >= 10 SELECT 10 AS Id, N'set-10' AS Name;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_multi_cache_policy
                                           @mode int
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           UPDATE dbo.CaeriusTestCounters SET Value = Value + 1 WHERE Id = 2;
                                           SELECT 1 AS Id, N'set-1' AS Name;
                                           IF @mode = 2 SELECT 2 AS Id, N'set-2' AS Name;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_read_cache_policy_counter
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           SELECT Value FROM dbo.CaeriusTestCounters WHERE Id = 2;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_increment_counter
                                           @id int
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           UPDATE dbo.CaeriusTestCounters SET Value = Value + 1 WHERE Id = @id;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_read_counter
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           SELECT Value FROM dbo.CaeriusTestCounters WHERE Id = 1;
                                       END
                                       """, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE OR ALTER PROCEDURE dbo.caerius_test_wait_scalar
                                       AS
                                       BEGIN
                                           SET NOCOUNT ON;
                                           WAITFOR DELAY '00:00:01';
                                           SELECT 1 AS Value;
                                       END
                                       """, cancellationToken);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string text, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = text;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record Widget(int Id, string Name) : ISpMapper<Widget>
    {
        public static Widget MapFromDataReader(SqlDataReader reader)
        {
            return new Widget(reader.GetInt32(0), reader.GetString(1));
        }
    }

    private sealed record SqlDecimalRow(SqlDecimal Value) : ISpMapper<SqlDecimalRow>
    {
        public static SqlDecimalRow MapFromDataReader(SqlDataReader reader)
        {
            return new SqlDecimalRow(reader.GetSqlDecimal(0));
        }
    }

    private readonly record struct IntRow(int Value) : ITvpMapper<IntRow>
    {
        private static readonly SqlMetaData[] SqlMetadata = [new("Value", SqlDbType.Int)];

        public static string SqlTypeName => "dbo.CaeriusTestIntList";
        public static ImmutableArray<SqlMetaData> Metadata => [.. SqlMetadata];

        public static SqlDataRecord CreateRecord()
        {
            return new SqlDataRecord(SqlMetadata);
        }

        public static void WriteRow(SqlDataRecord record, IntRow row)
        {
            record.SetInt32(0, row.Value);
        }
    }

    private sealed class OneShotRows(int count) : IEnumerable<IntRow>
    {
        private int _enumerationCount;
        private int _rowsYielded;

        public int EnumerationCount => Volatile.Read(ref _enumerationCount);
        public int RowsYielded => Volatile.Read(ref _rowsYielded);

        public IEnumerator<IntRow> GetEnumerator()
        {
            if (Interlocked.Increment(ref _enumerationCount) != 1)
                throw new InvalidOperationException("The lazy TVP source must be enumerated exactly once.");
            return Enumerate().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private IEnumerable<IntRow> Enumerate()
        {
            for (var value = 1; value <= count; value++)
            {
                Interlocked.Increment(ref _rowsYielded);
                yield return new IntRow(value);
            }
        }
    }

    private sealed class RecordingRemoteCache : ICaeriusRemoteCacheManager
    {
        private readonly List<string> _removedKeys = [];
        private CancellationTokenSource? _cancellationProbe;

        public IReadOnlyList<string> RemovedKeys
        {
            get
            {
                lock (_removedKeys)
                {
                    return _removedKeys.ToArray();
                }
            }
        }

        public ValueTask<CaeriusCacheLookup<T>> TryGetAsync<T>(
            string key,
            ICaeriusCacheCodec<T> codec,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(CaeriusCacheLookup<T>.Miss);
        }

        public ValueTask SetAsync<T>(
            string key,
            T value,
            ICaeriusCacheCodec<T> codec,
            TimeSpan? expiration,
            CancellationToken cancellationToken = default) where T : notnull
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            CancellationTokenSource? cancellationProbe;
            lock (_removedKeys)
            {
                _removedKeys.Add(key);
                cancellationProbe = _cancellationProbe;
                _cancellationProbe = null;
            }

            if (cancellationProbe is not null)
            {
                cancellationProbe.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return ValueTask.CompletedTask;
        }

        public void CancelAndRequireNonCancelableRemoval(CancellationTokenSource cancellationProbe)
        {
            ArgumentNullException.ThrowIfNull(cancellationProbe);
            lock (_removedKeys)
            {
                _cancellationProbe = cancellationProbe;
            }
        }

        public void ClearCancellationProbe()
        {
            lock (_removedKeys)
            {
                _cancellationProbe = null;
            }
        }

        public void Clear()
        {
            lock (_removedKeys)
            {
                _removedKeys.Clear();
            }
        }
    }
}
