using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using CaeriusNet.IntegrationTests.Infrastructure;

namespace CaeriusNet.IntegrationTests.Redis;

public sealed class RedisCacheAcceptanceTests
{
    [Fact]
    public async Task SetGetRemoveAndExpiryUseOneTypedRedisValue()
    {
        ContainerTestGate.RequireEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var container = new RedisBuilder("redis:7.4-alpine").Build();
        await container.StartAsync(cancellationToken);

        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>(
            await ConnectionMultiplexer.ConnectAsync(container.GetConnectionString()));
        services.AddCaeriusNetRedis();
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IRedisCacheManager>();
        var database = provider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var codec = new Int32Codec("widget", "v1");

        await cache.SetAsync("lifecycle", 42, codec, TimeSpan.FromMilliseconds(180), cancellationToken);
        var hit = await cache.TryGetAsync("lifecycle", codec, cancellationToken);
        Assert.True(hit.Found);
        Assert.Equal(42, hit.Value);
        Assert.True(await database.KeyTimeToLiveAsync("lifecycle") is { } ttl && ttl > TimeSpan.Zero);

        await cache.RemoveAsync("lifecycle", cancellationToken);
        Assert.False((await cache.TryGetAsync("lifecycle", codec, cancellationToken)).Found);

        await cache.SetAsync("expiry", 8, codec, TimeSpan.FromMilliseconds(120), cancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
        Assert.False((await cache.TryGetAsync("expiry", codec, cancellationToken)).Found);
    }

    [Fact]
    public async Task CorruptAndContractIncompatiblePayloadsAreControlledMissesAndAreRemoved()
    {
        ContainerTestGate.RequireEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;
        var corruptionMeasurements = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == CaeriusRedisTelemetry.MeterName &&
                    instrument.Name == "caerius.cache.corruptions")
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            corruptionMeasurements.Enqueue(tags.ToArray()));
        meterListener.Start();

        await using var container = new RedisBuilder("redis:7.4-alpine").Build();
        await container.StartAsync(cancellationToken);
        var connectionString = container.GetConnectionString();

        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>(await ConnectionMultiplexer.ConnectAsync(connectionString));
        services.AddCaeriusNetRedis();
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IRedisCacheManager>();
        var multiplexer = provider.GetRequiredService<IConnectionMultiplexer>();
        var database = multiplexer.GetDatabase();

        var versionOne = new Int32Codec("widget", "v1");
        await cache.SetAsync("incompatible", 42, versionOne, TimeSpan.FromMinutes(1), cancellationToken);
        var incompatible = await cache.TryGetAsync("incompatible", new Int32Codec("widget", "v2"), cancellationToken);
        Assert.False(incompatible.Found);
        Assert.False(await database.KeyExistsAsync("incompatible"));

        await database.StringSetAsync("corrupt", new byte[] { 0x00, 0x01, 0x02 });
        var corrupt = await cache.TryGetAsync("corrupt", versionOne, cancellationToken);
        Assert.False(corrupt.Found);
        Assert.False(await database.KeyExistsAsync("corrupt"));

        // Valid magic but an unsupported envelope version is also an incompatible payload, not a cache exception.
        var unsupportedVersion = new byte[13];
        BinaryPrimitives.WriteUInt32LittleEndian(unsupportedVersion, 0x54454E43);
        unsupportedVersion[4] = 2;
        await database.StringSetAsync("unsupported-version", unsupportedVersion);
        var unsupported = await cache.TryGetAsync("unsupported-version", versionOne, cancellationToken);
        Assert.False(unsupported.Found);
        Assert.False(await database.KeyExistsAsync("unsupported-version"));

        var brokenDecoder = new ThrowingDecodeInt32Codec("broken-decoder", "v1");
        await cache.SetAsync("decoder-fault", 17, brokenDecoder, TimeSpan.FromMinutes(1), cancellationToken);
        var decoderFault = await cache.TryGetAsync("decoder-fault", brokenDecoder, cancellationToken);
        Assert.False(decoderFault.Found);
        Assert.False(await database.KeyExistsAsync("decoder-fault"));

        // Redis owns its corruption metric under its own Meter identity. A cache key, envelope bytes, or codec value
        // must never become a metric dimension; each invalid payload only increments the low-cardinality counter.
        Assert.Equal(4, corruptionMeasurements.Count);
        Assert.All(corruptionMeasurements, static tags => Assert.Empty(tags));
    }

    [Fact]
    public async Task PayloadLimitRejectsOversizedWritesAndEvictsOversizedReads()
    {
        ContainerTestGate.RequireEnabled();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var container = new RedisBuilder("redis:7.4-alpine").Build();
        await container.StartAsync(cancellationToken);
        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>(
            await ConnectionMultiplexer.ConnectAsync(container.GetConnectionString()));
        services.AddCaeriusNetRedis(options => options.MaxPayloadBytes = 32);
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IRedisCacheManager>();
        var database = provider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var codec = new Int32Codec("widget", "v1");

        var writeException = await Assert.ThrowsAsync<CaeriusCacheException>(() =>
            cache.SetAsync("oversized-write", 42, new OversizedInt32Codec(), TimeSpan.FromMinutes(1), cancellationToken)
                .AsTask());
        Assert.Equal("payload-limit", writeException.Operation);
        Assert.False(await database.KeyExistsAsync("oversized-write"));

        await database.StringSetAsync("oversized-read", new byte[33]);
        var oversized = await cache.TryGetAsync("oversized-read", codec, cancellationToken);
        Assert.False(oversized.Found);
        Assert.False(await database.KeyExistsAsync("oversized-read"));
    }

    private sealed class Int32Codec(string codecId, string contractHash) : ICaeriusCacheCodec<int>
    {
        public string CodecId { get; } = codecId;
        public string ContractHash { get; } = contractHash;

        public void Encode(IBufferWriter<byte> writer, int value)
        {
            var binary = new CaeriusBinaryWriter(writer);
            binary.WriteInt32(value);
        }

        public int Decode(ReadOnlySpan<byte> payload)
        {
            var binary = new CaeriusBinaryReader(payload);
            var value = binary.ReadInt32();
            if (binary.Remaining != 0)
                throw new CaeriusContractException("The Int32 cache payload contains trailing bytes.");
            return value;
        }
    }

    private sealed class ThrowingDecodeInt32Codec(string codecId, string contractHash) : ICaeriusCacheCodec<int>
    {
        public string CodecId { get; } = codecId;
        public string ContractHash { get; } = contractHash;

        public void Encode(IBufferWriter<byte> writer, int value)
        {
            var binary = new CaeriusBinaryWriter(writer);
            binary.WriteInt32(value);
        }

        public int Decode(ReadOnlySpan<byte> payload)
        {
            throw new OverflowException("Deliberately malformed decoder payload.");
        }
    }

    private sealed class OversizedInt32Codec : ICaeriusCacheCodec<int>
    {
        public string CodecId => "oversized";
        public string ContractHash => "v1";

        public void Encode(IBufferWriter<byte> writer, int value)
        {
            writer.Write(new byte[64]);
        }

        public int Decode(ReadOnlySpan<byte> payload)
        {
            throw new NotSupportedException("The payload-limit test never decodes its deliberately oversized value.");
        }
    }
}
