using System.Buffers;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Reflection;
using CaeriusNet.Abstractions;
using CaeriusNet;
using CaeriusNet.Caching;
using CaeriusNet.Commands;
using Microsoft.Extensions.DependencyInjection;
using CaeriusNet.Exceptions;
using CaeriusNet.Execution;
using CaeriusNet.Internal;
using CaeriusNet.Results;
using CaeriusNet.Telemetry;
using CaeriusNet.Tests.Observability;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;
using Microsoft.Extensions.Options;
using Xunit;

namespace CaeriusNet.Tests.Runtime;

[Collection(TelemetryTestScope.Name)]
public sealed class RuntimeContractTests
{
    [Fact]
    public void BuilderCreatesAnImmutableStoredProcedureContract()
    {
        var command = new StoredProcedureCommandBuilder("dbo", "FindCustomer", 12)
            .AddParameter("id", 42, SqlDbType.Int, ordinal: 0)
            .AddOutputParameter("name", SqlDbType.NVarChar, 128, ordinal: 1)
            .WithResultSetCapacities(4, 8)
            .Build();

        Assert.Equal("dbo.FindCustomer", command.FullName);
        Assert.Equal(12, command.CommandTimeout);
        Assert.Equal([4, 8], command.ResultSetCapacities);
        Assert.Equal(ParameterDirection.Output, command.Parameters[1].Direction);
        Assert.Equal(128, command.Parameters[1].Size);
        Assert.StartsWith("@", command.Parameters[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void BuilderRejectsInvalidContractsBeforeOpeningAConnection()
    {
        var duplicate = new StoredProcedureCommandBuilder("dbo", "Duplicate")
            .AddParameter("value", 1, SqlDbType.Int, ordinal: 0)
            .AddParameter("VALUE", 2, SqlDbType.Int, ordinal: 1);
        Assert.Throws<CaeriusContractException>(() => duplicate.Build());
        Assert.Throws<ArgumentException>(() => new StoredProcedureCommandBuilder("dbo;DROP", "Unsafe").Build());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StoredProcedureCommandBuilder("dbo", "Timeout", 0).Build());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StoredProcedureCommandBuilder("dbo", "InvalidType")
                .AddParameter("value", 1, (SqlDbType)int.MaxValue));
    }

    [Fact]
    public void BuilderRequiresExplicitFacetsForOutputInputOutputAndNullParameters()
    {
        Assert.Throws<CaeriusContractException>(() =>
            new StoredProcedureCommandBuilder("dbo", "OutputText")
                .AddOutputParameter("text", SqlDbType.NVarChar));
        Assert.Throws<CaeriusContractException>(() =>
            new StoredProcedureCommandBuilder("dbo", "NullText")
                .AddParameter("text", null, SqlDbType.VarChar));
        Assert.Throws<CaeriusContractException>(() =>
            new StoredProcedureCommandBuilder("dbo", "DbNullText")
                .AddParameter("text", DBNull.Value, SqlDbType.VarChar));
        Assert.Throws<CaeriusContractException>(() =>
            new StoredProcedureCommandBuilder("dbo", "SqlNullText")
                .AddParameter("text", SqlString.Null, SqlDbType.NVarChar));
        Assert.Throws<CaeriusContractException>(() =>
            new StoredProcedureCommandBuilder("dbo", "InputOutputDecimal")
                .AddInputOutputParameter("amount", 1.5m, SqlDbType.Decimal));
        Assert.Throws<CaeriusContractException>(() =>
            new StoredProcedureCommandBuilder("dbo", "NullDateTime2")
                .AddParameter("createdAt", null, SqlDbType.DateTime2));

        var command = new StoredProcedureCommandBuilder("dbo", "FacetedParameters")
            .AddOutputParameter("text", SqlDbType.NVarChar, 128)
            .AddParameter("nullableText", null, SqlDbType.VarBinary, size: -1)
            .AddInputOutputParameter("amount", 1.5m, SqlDbType.Decimal, precision: 18, scale: 4)
            .AddParameter("createdAt", null, SqlDbType.DateTime2, scale: 7)
            .Build();

        Assert.Equal(4, command.Parameters.Length);
        Assert.Equal(128, command.Parameters[0].Size);
        Assert.Equal((byte)18, command.Parameters[2].Precision);
        Assert.Equal((byte)7, command.Parameters[3].Scale);
    }

    [Fact]
    public void ScalarContractKeepsNoRowAndSqlNullSemanticsDistinct()
    {
        var command = new StoredProcedureCommandBuilder("dbo", "Scalar").Build();

        var noRow = Assert.Throws<CaeriusContractException>(() =>
            CaeriusExecutor.ConvertRequiredScalar<int>(CaeriusScalarRow.None, command));
        var sqlNull = Assert.Throws<CaeriusContractException>(() =>
            CaeriusExecutor.ConvertRequiredScalar<int>(new CaeriusScalarRow(true, DBNull.Value), command));

        Assert.Contains("no scalar row", noRow.Message, StringComparison.Ordinal);
        Assert.Contains("SQL NULL", sqlNull.Message, StringComparison.Ordinal);
        Assert.Equal(0, CaeriusExecutor.ConvertOptionalScalar<int>(CaeriusScalarRow.None, command));
        Assert.Null(CaeriusExecutor.ConvertOptionalScalar<string?>(new CaeriusScalarRow(true, DBNull.Value), command));
        Assert.Throws<CaeriusContractException>(() =>
            CaeriusExecutor.ConvertOptionalScalar<int>(new CaeriusScalarRow(true, DBNull.Value), command));
    }

    [Fact]
    public void GeneratedTvpContractSupportsAnEmptySequence()
    {
        var command = new StoredProcedureCommandBuilder("dbo", "ConsumeRows")
            .AddTvpParameter("rows", Array.Empty<GeneratedTvpRow>(), ordinal: 0)
            .Build();

        Assert.Equal("dbo.GeneratedRows", GeneratedTvpRow.SqlTypeName);
        Assert.Equal(4, GeneratedTvpRow.Metadata.Length);
        Assert.Equal(SqlDbType.Structured, command.Parameters[0].DbType);
        Assert.Equal("dbo.GeneratedRows", command.Parameters[0].TypeName);
    }

    [Fact]
    public void CacheKeysIncludeCanonicalTvpRowsAndReplayOneShotSequences()
    {
        var enumerationCount = 0;

        IEnumerable<GeneratedTvpRow> Rows(int value)
        {
            enumerationCount++;
            yield return new GeneratedTvpRow(value);
        }

        var first = new StoredProcedureCommandBuilder("dbo", "ConsumeRows")
            .AddTvpParameter("rows", Rows(1))
            .UseRedisCache()
            .Build();
        var second = new StoredProcedureCommandBuilder("dbo", "ConsumeRows")
            .AddTvpParameter("rows", [new GeneratedTvpRow(2)])
            .UseRedisCache()
            .Build();
        var empty = new StoredProcedureCommandBuilder("dbo", "ConsumeRows")
            .AddTvpParameter("rows", Array.Empty<GeneratedTvpRow>())
            .UseRedisCache()
            .Build();

        var firstKey = CaeriusCacheKeyBuilder.Create("tests", "reporting", first, "immutable-array");
        Assert.Equal(firstKey, CaeriusCacheKeyBuilder.Create("tests", "reporting", first, "immutable-array"));
        Assert.NotEqual(firstKey, CaeriusCacheKeyBuilder.Create("tests", "reporting", second, "immutable-array"));
        Assert.NotEqual(firstKey, CaeriusCacheKeyBuilder.Create("tests", "reporting", empty, "immutable-array"));
        Assert.NotEqual(firstKey, CaeriusCacheKeyBuilder.Create("tests", "billing", first, "immutable-array"));
        Assert.NotEqual(
            CaeriusCacheKeyBuilder.Create("tests", string.Empty, first, "immutable-array"),
            CaeriusCacheKeyBuilder.Create("tests", "default", first, "immutable-array"));

        using var connection = new SqlConnection();
        using var sqlCommand = first.CreateSqlCommand(connection);
        var records = Assert.IsAssignableFrom<IEnumerable<SqlDataRecord>>(sqlCommand.Parameters[0].Value);
        Assert.Equal(1, Assert.Single(records).GetInt32(0));

        using var emptySqlCommand = empty.CreateSqlCommand(connection);
        Assert.Null(emptySqlCommand.Parameters[0].Value);
        Assert.Equal(1, enumerationCount);
    }

    [Fact]
    public void CacheKeysCanonicallyEncodeEverySupportedBinaryParameterRepresentation()
    {
        static string Key(object value)
        {
            return CaeriusCacheKeyBuilder.Create(
                "tests",
                "cache",
                new StoredProcedureCommandBuilder("dbo", "BinaryValue")
                    .AddParameter("payload", value, SqlDbType.VarBinary)
                    .Build(),
                "immutable-array");
        }

        var firstMemory = new Memory<byte>([0x01, 0x02, 0x03]);
        var secondMemory = new Memory<byte>([0x07, 0x08, 0x09]);
        Assert.NotEqual(Key(firstMemory), Key(secondMemory));
        Assert.NotEqual(Key(new ReadOnlyMemory<byte>([0x01, 0x02, 0x03])),
            Key(new ReadOnlyMemory<byte>([0x07, 0x08, 0x09])));
        Assert.NotEqual(Key(new ArraySegment<byte>([0x01, 0x02, 0x03])),
            Key(new ArraySegment<byte>([0x07, 0x08, 0x09])));
        Assert.NotEqual(Key(new SqlBytes([0x01, 0x02, 0x03])), Key(new SqlBytes([0x07, 0x08, 0x09])));

        var stable = Key(new Memory<byte>([0x01, 0x02, 0x03]));
        Assert.Equal(stable, Key(new Memory<byte>([0x01, 0x02, 0x03])));
    }

    [Fact]
    public void CacheKeyRejectsUnsupportedParameterRepresentationsInsteadOfUsingToString()
    {
        var command = new StoredProcedureCommandBuilder("dbo", "UnsupportedParameter")
            .AddParameter("payload", new object(), SqlDbType.Variant)
            .Build();

        var exception = Assert.Throws<CaeriusContractException>(() =>
            CaeriusCacheKeyBuilder.Create("tests", "cache", command, "immutable-array"));
        Assert.Contains("cannot canonically encode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedCodecRoundtripsNullableValuesAndCollections()
    {
        var codec = new GeneratedDtoCaeriusCacheCodec();
        var source = new GeneratedDto(7, null, new DateOnly(2026, 8, 14));
        var buffer = new ArrayBufferWriter<byte>();
        codec.Encode(buffer, source);
        Assert.Equal(source, codec.Decode(buffer.WrittenSpan));

        var services = new ServiceCollection();
        services.AddGeneratedDtoCaeriusCodec();
        using var provider = services.BuildServiceProvider();
        var collectionCodec = provider.GetRequiredService<ICaeriusCacheCodec<ImmutableArray<GeneratedDto>>>();
        var collectionBuffer = new ArrayBufferWriter<byte>();
        collectionCodec.Encode(collectionBuffer, [source]);
        Assert.Equal([source], collectionCodec.Decode(collectionBuffer.WrittenSpan));
    }

    [Fact]
    public void BinaryCodecPrimitivesRoundtripGuidAndDecimalWithoutArrayBasedEncoding()
    {
        var expectedGuid = Guid.Parse("d4317289-3bc6-4b47-a0b8-019017f2b5c7");
        const decimal expectedDecimal = -123456789.0123456789012345678m;
        var writer = new ArrayBufferWriter<byte>();
        var binaryWriter = new CaeriusBinaryWriter(writer);
        binaryWriter.WriteGuid(expectedGuid);
        binaryWriter.WriteDecimal(expectedDecimal);

        var reader = new CaeriusBinaryReader(writer.WrittenSpan);
        Assert.Equal(expectedGuid, reader.ReadGuid());
        Assert.Equal(expectedDecimal, reader.ReadDecimal());
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void BinaryCodecPrimitiveFramingIsLittleEndianAndLengthDelimited()
    {
        var payload = new ArrayBufferWriter<byte>();
        var writer = new CaeriusBinaryWriter(payload);
        writer.WriteInt32(0x10203040);
        writer.WriteBytes([0xA1, 0xB2]);

        Assert.Equal(
            [0x40, 0x30, 0x20, 0x10, 0x02, 0x00, 0x00, 0x00, 0xA1, 0xB2],
            payload.WrittenSpan.ToArray());
    }

    [Fact]
    public void CollectionCodecRejectsAnUnboundedDeclaredCountBeforeAllocating()
    {
        var services = new ServiceCollection();
        services.AddGeneratedDtoCaeriusCodec();
        using var provider = services.BuildServiceProvider();
        var codec = provider.GetRequiredService<ICaeriusCacheCodec<ImmutableArray<GeneratedDto>>>();
        var payload = new ArrayBufferWriter<byte>();
        new CaeriusBinaryWriter(payload).WriteInt32(1_000_001);

        Assert.Throws<CaeriusContractException>(() => codec.Decode(payload.WrittenSpan));
    }

    [Fact]
    public void ManualCodecRegistrationAddsTheThreeMaterializedQueryCodecs()
    {
        var services = new ServiceCollection();
        services.AddCaeriusNet(options =>
            options.UseSqlServer("Server=.;Database=cache;Integrated Security=true;TrustServerCertificate=true"));
        services.AddCaeriusCacheCodec<ManualCacheDto, ManualCacheDtoCodec>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = (ICaeriusExecutionContext)scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var source = new ManualCacheDto(42);

        var arrayCodec = provider.GetRequiredService<ICaeriusCacheCodec<ManualCacheDto[]>>();
        var readOnlyCodec = provider.GetRequiredService<ICaeriusCacheCodec<ReadOnlyCollection<ManualCacheDto>>>();
        var immutableCodec = provider.GetRequiredService<ICaeriusCacheCodec<ImmutableArray<ManualCacheDto>>>();

        Assert.Equal([source], RoundTrip(arrayCodec, [source]));
        Assert.Equal([source], RoundTrip(readOnlyCodec, new ReadOnlyCollection<ManualCacheDto>([source])));
        Assert.Equal([source], RoundTrip(immutableCodec, [source]));
        Assert.NotNull(CaeriusExecutor.FindCodec<ManualCacheDto[]>(context));
        Assert.NotNull(CaeriusExecutor.FindCodec<ReadOnlyCollection<ManualCacheDto>>(context));
        Assert.NotNull(CaeriusExecutor.FindCodec<ImmutableArray<ManualCacheDto>>(context));
    }

    [Fact]
    public void AddCaeriusNetIsIdempotentAndNamedContextsAreIsolated()
    {
        var services = new ServiceCollection();
        services.AddCaeriusNet(options =>
            options.UseSqlServer("Server=.;Database=main;Integrated Security=true;TrustServerCertificate=true"));
        services.AddCaeriusNet(options =>
            options.UseSqlServer("Server=.;Database=main;Integrated Security=true;TrustServerCertificate=true"));
        services.AddCaeriusNet("reporting", options =>
            options.UseSqlServer("Server=.;Database=reporting;Integrated Security=true;TrustServerCertificate=true"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var primary = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var reporting = scope.ServiceProvider.GetRequiredKeyedService<ICaeriusNetDbContext>("reporting");
        Assert.Equal(Options.DefaultName, primary.Name);
        Assert.Equal("reporting", reporting.Name);
        Assert.NotSame(primary, reporting);
        Assert.NotEqual(
            ((ICaeriusExecutionContext)primary).Options.CacheDatabaseIdentity,
            ((ICaeriusExecutionContext)reporting).Options.CacheDatabaseIdentity);
    }

    [Fact]
    public void CacheDatabaseIdentityUsesOnlyTheSqlEndpointAndCatalogAndRejectsConnectionStrings()
    {
        var first = new CaeriusNetOptions()
            .UseSqlServer(
                "Server=tcp:sql-a,1433;Database=orders;User ID=sa;Password=never-cache-this;TrustServerCertificate=true");
        var equivalent = new CaeriusNetOptions()
            .UseSqlServer(
                "Data Source=tcp:sql-a,1433;Initial Catalog=orders;Integrated Security=true;Application Name=ignored");

        Assert.Equal(first.CacheDatabaseIdentity, equivalent.CacheDatabaseIdentity);
        Assert.DoesNotContain("Password", first.CacheDatabaseIdentity!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("never-cache-this", first.CacheDatabaseIdentity!, StringComparison.Ordinal);

        var invalid = new CaeriusNetOptions
        {
            CacheDatabaseIdentity = "Server=sql-a;Database=orders;Password=never-cache-this"
        }.UseSqlServer("Server=sql-a;Database=orders;Integrated Security=true");
        var exception = Assert.Throws<ArgumentException>(() => invalid.Validate("default"));
        Assert.Contains("non-secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CacheKeyHashesAndSeparatesDatabaseIdentities()
    {
        var command = new StoredProcedureCommandBuilder("dbo", "GetOrder")
            .AddParameter("id", 7, SqlDbType.Int)
            .Build();

        var first = CaeriusCacheKeyBuilder.Create("orders", "sqlserver|12:server-alpha|6:orders", command,
            "query:ManualCacheDto[]");
        var second = CaeriusCacheKeyBuilder.Create("orders", "sqlserver|11:server-beta|6:orders", command,
            "query:ManualCacheDto[]");

        Assert.NotEqual(first, second);
        Assert.DoesNotContain("server-alpha", first, StringComparison.Ordinal);
        Assert.DoesNotContain("orders", first.Split(':')[1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StatefulInMemoryCacheFactoryPreservesItsValueTaskAndCancellationContracts()
    {
        var services = new ServiceCollection();
        services.AddCaeriusNet(options =>
            options.UseSqlServer("Server=.;Database=cache;Integrated Security=true;TrustServerCertificate=true"));
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IInMemoryCacheManager>();

        var result = await cache.GetOrCreateAsync(
            "stateful-factory",
            TimeSpan.FromMinutes(1),
            false,
            73,
            static (state, _) => ValueTask.FromResult(state),
            TestContext.Current.CancellationToken);

        Assert.Equal(73, result);
    }

    [Fact]
    public async Task RedisReadThroughUsesDifferentOpaqueKeysForDifferentHostDatabases()
    {
        var remote = new RecordingRemoteCacheManager();
        using var firstProvider = CreateRedisReadThroughProvider(
            "Server=server-alpha;Database=orders;Integrated Security=true;TrustServerCertificate=true",
            remote);
        using var secondProvider = CreateRedisReadThroughProvider(
            "Server=server-beta;Database=orders;Integrated Security=true;TrustServerCertificate=true",
            remote);
        using var firstScope = firstProvider.CreateScope();
        using var secondScope = secondProvider.CreateScope();
        var firstContext =
            (ICaeriusExecutionContext)firstScope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var secondContext =
            (ICaeriusExecutionContext)secondScope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var command = new StoredProcedureCommandBuilder("dbo", "GetOrder")
            .AddParameter("id", 7, SqlDbType.Int)
            .UseRedisCache(TimeSpan.FromMinutes(1))
            .Build();

        var first = await CaeriusExecutor.CachedAsync<ManualCacheDto, ManualCacheDto>(
            firstContext,
            command,
            "query:ManualCacheDto",
            new ManualCacheDto(1),
            static (value, _) => ValueTask.FromResult(value),
            TestContext.Current.CancellationToken);
        var second = await CaeriusExecutor.CachedAsync<ManualCacheDto, ManualCacheDto>(
            secondContext,
            command,
            "query:ManualCacheDto",
            new ManualCacheDto(2),
            static (value, _) => ValueTask.FromResult(value),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, first.Id);
        Assert.Equal(2, second.Id);
        Assert.Equal(2, remote.LookupKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, remote.SetKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(remote.LookupKeys, static key => Assert.DoesNotContain("server-", key, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RedisReadThroughRequiresAnExplicitIdentityForAConnectionFactory()
    {
        var remote = new RecordingRemoteCacheManager();
        var services = new ServiceCollection();
        services.AddCaeriusNet(options => options.UseSqlServer(_ => new SqlConnection(
            "Server=server-alpha;Database=orders;Integrated Security=true;TrustServerCertificate=true")));
        services.AddCaeriusCacheCodec<ManualCacheDto, ManualCacheDtoCodec>();
        services.AddSingleton<ICaeriusRemoteCacheManager>(remote);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = (ICaeriusExecutionContext)scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var command = new StoredProcedureCommandBuilder("dbo", "GetOrder")
            .UseRedisCache(TimeSpan.FromMinutes(1))
            .Build();

        var exception = await Assert.ThrowsAsync<CaeriusContractException>(() =>
            CaeriusExecutor.CachedAsync<ManualCacheDto, ManualCacheDto>(
                context,
                command,
                "query:ManualCacheDto",
                new ManualCacheDto(1),
                static (value, _) => ValueTask.FromResult(value),
                TestContext.Current.CancellationToken).AsTask());

        Assert.Contains("CacheDatabaseIdentity", exception.Message, StringComparison.Ordinal);
        Assert.Empty(remote.LookupKeys);
    }

    [Fact]
    public void MultiResultSurfaceHasEveryArityAndCollectionFamilyForBothContextKinds()
    {
        var allMethods = typeof(CaeriusNetMultiResultSetExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static);

        var contextMethods = allMethods
            .Where(static method => method.GetParameters()[0].ParameterType == typeof(ICaeriusNetDbContext))
            .ToArray();
        var transactionalMethods = allMethods
            .Where(static method => method.GetParameters()[0].ParameterType == typeof(ICaeriusNetTransaction))
            .ToArray();

        AssertMultiResultSurface(contextMethods);
        AssertMultiResultSurface(transactionalMethods);
    }

    private static void AssertMultiResultSurface(MethodInfo[] methods)
    {
        Assert.Equal(54, methods.Length);
        Assert.Equal(9, methods.Count(static method => method.Name == "QueryMultipleAsync"));
        Assert.Equal(9, methods.Count(static method => method.Name == "QueryMultipleReadOnlyCollectionAsync"));
        Assert.Equal(9, methods.Count(static method => method.Name == "QueryMultipleImmutableArrayAsync"));
        Assert.Equal(9, methods.Count(static method => method.Name == "QueryMultipleWithOutputsAsync"));
        Assert.Equal(9,
            methods.Count(static method => method.Name == "QueryMultipleReadOnlyCollectionWithOutputsAsync"));
        Assert.Equal(9, methods.Count(static method => method.Name == "QueryMultipleImmutableArrayWithOutputsAsync"));
    }

    [Fact]
    public void MultiResultCacheShapeSeparatesEveryResultSetCountPolicy()
    {
        MultiResultSetExecutor.ResultSetReader reader = static (_, _, _, _, _) =>
            ValueTask.FromResult<object>(Array.Empty<object>());
        var plan = new MultiResultSetPlan(
            [reader, reader],
            [static () => Array.Empty<object>(), static () => Array.Empty<object>()],
            [typeof(object[]), typeof(object[])],
            "immutable-array:2:System.Object|System.Object");

        var shapes = Enum.GetValues<CaeriusResultSetCountPolicy>()
            .Select(plan.GetCacheShape)
            .ToArray();

        Assert.Equal(4, shapes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("immutable-array:2:System.Object|System.Object", shapes[0]);
        Assert.Contains("result-set-policy:allow-missing", shapes[1], StringComparison.Ordinal);
        Assert.Contains("result-set-policy:allow-additional", shapes[2], StringComparison.Ordinal);
        Assert.Contains("result-set-policy:allow-missing-or-additional", shapes[3], StringComparison.Ordinal);
    }

    [Fact]
    public async Task InMemoryCacheSingleFlightRunsOneFactory()
    {
        var services = new ServiceCollection();
        services.AddCaeriusNet(options =>
            options.UseSqlServer("Server=.;Database=cache;Integrated Security=true;TrustServerCertificate=true"));
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IInMemoryCacheManager>();
        var calls = 0;

        async ValueTask<int> Factory(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return 42;
        }

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => cache.GetOrCreateAsync(
                "single",
                TimeSpan.FromMinutes(1),
                false,
                Factory,
                TestContext.Current.CancellationToken).AsTask())
            .ToArray();
        var values = await Task.WhenAll(tasks);
        Assert.All(values, value => Assert.Equal(42, value));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task InMemoryCacheSingleFlightDoesNotCaptureTheFirstWaitersCancellation()
    {
        var services = new ServiceCollection();
        services.AddCaeriusNet(options =>
            options.UseSqlServer("Server=.;Database=cache;Integrated Security=true;TrustServerCertificate=true"));
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IInMemoryCacheManager>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        async ValueTask<int> Factory(CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            Assert.False(token.CanBeCanceled);
            started.TrySetResult();
            await release.Task;
            return 42;
        }

        using var firstWaiterCancellation = new CancellationTokenSource();
        var first = cache.GetOrCreateAsync(
            "cancelled-single-flight",
            TimeSpan.FromMinutes(1),
            false,
            Factory,
            firstWaiterCancellation.Token).AsTask();
        await started.Task;
        firstWaiterCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        var second = cache.GetOrCreateAsync(
            "cancelled-single-flight",
            TimeSpan.FromMinutes(1),
            false,
            _ => ValueTask.FromResult(-1),
            TestContext.Current.CancellationToken);
        release.TrySetResult();

        Assert.Equal(42, await second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ParameterCaptureExportsOnlyAllowlistedRedactedNonTvpInputs()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == CaeriusTelemetry.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        var command = new StoredProcedureCommandBuilder("dbo", "Capture")
            .AddParameter("visible", "secret", SqlDbType.NVarChar)
            .AddParameter("hidden", "never-exported", SqlDbType.NVarChar)
            .AddTvpParameter("rows", [new GeneratedTvpRow(9)])
            .Build();
        var options = new CaeriusNetOptions
            { CaptureParameterValues = true, ParameterRedactor = static (_, _) => "[redacted]" };
        options.ParameterCaptureAllowList.Add("visible");
        options.ParameterCaptureAllowList.Add("rows");

        using var instrumentation = new CaeriusInstrumentation();
        using var connection = new SqlConnection("Data Source=localhost;Initial Catalog=caerius");
        using (var operation = instrumentation.Start(
                   command,
                   connection,
                   false,
                   options))
        {
            operation.Success();
        }

        var tags = Assert.Single(stopped).Tags.ToDictionary(static tag => tag.Key, static tag => tag.Value);
        Assert.Equal("[redacted]", tags["caerius.db.parameter.visible"]);
        Assert.DoesNotContain("caerius.db.parameter.hidden", tags.Keys);
        Assert.DoesNotContain("caerius.db.parameter.rows", tags.Keys);
    }

    [Fact]
    public async Task RedisPolicyWithoutARemoteManagerFailsBeforeOpeningSql()
    {
        var services = new ServiceCollection();
        services.AddCaeriusNet(options =>
            options.UseSqlServer("Server=invalid;Database=cache;Integrated Security=true;TrustServerCertificate=true"));
        services.AddGeneratedDtoCaeriusCodec();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var command = new StoredProcedureCommandBuilder("dbo", "RedisOnly")
            .UseRedisCache()
            .Build();

        var exception = await Assert.ThrowsAsync<CaeriusContractException>(() =>
            context.QueryImmutableArrayAsync<GeneratedDto>(
                command,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("IRedisCacheManager", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StrictInvalidationUsesEachDeclaredRemoteKeyOnce()
    {
        var remote = new RecordingRemoteCacheManager();
        var services = new ServiceCollection();
        services.AddCaeriusNet(options =>
            options.UseSqlServer("Server=.;Database=cache;Integrated Security=true;TrustServerCertificate=true"));
        services.AddSingleton<ICaeriusRemoteCacheManager>(remote);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = (ICaeriusExecutionContext)scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();

        await CaeriusExecutor.InvalidateStrictAsync(
            context,
            ["after-output", "after-output", "dependent-output"],
            TestContext.Current.CancellationToken);

        Assert.Equal(["after-output", "dependent-output"], remote.RemovedKeys);
    }

    [Fact]
    public void FrozenCacheReplacesASnapshotAtomicallyAndDoesNotRebuildOnMiss()
    {
        var services = new ServiceCollection();
        services.AddCaeriusNet(options =>
            options.UseSqlServer("Server=.;Database=frozen;Integrated Security=true;TrustServerCertificate=true"));
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IFrozenCacheManager>();
        cache.ReplaceSnapshot(new[] { new KeyValuePair<string, int>("one", 1) });
        Assert.True(cache.TryGet<int>("one", out var one));
        Assert.Equal(1, one);
        Assert.False(cache.TryGet<int>("missing", out _));
        cache.ReplaceSnapshot(new[] { new KeyValuePair<string, int>("two", 2) });
        Assert.False(cache.TryGet<int>("one", out _));
        Assert.True(cache.TryGet<int>("two", out var two));
        Assert.Equal(2, two);
    }

    private static T RoundTrip<T>(ICaeriusCacheCodec<T> codec, T source)
    {
        var writer = new ArrayBufferWriter<byte>();
        codec.Encode(writer, source);
        return codec.Decode(writer.WrittenSpan);
    }

    private static ServiceProvider CreateRedisReadThroughProvider(
        string connectionString,
        RecordingRemoteCacheManager remote)
    {
        var services = new ServiceCollection();
        services.AddCaeriusNet(options =>
        {
            options.ApplicationNamespace = "shared-orders";
            options.UseSqlServer(connectionString);
        });
        services.AddCaeriusCacheCodec<ManualCacheDto, ManualCacheDtoCodec>();
        services.AddSingleton<ICaeriusRemoteCacheManager>(remote);
        return services.BuildServiceProvider();
    }
}
