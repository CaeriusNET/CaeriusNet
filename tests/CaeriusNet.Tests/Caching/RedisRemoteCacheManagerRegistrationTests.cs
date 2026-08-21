using System.Buffers;
using System.Collections.Immutable;
using CaeriusNet.Abstractions;
using CaeriusNet.Caching;
using CaeriusNet.Redis;
using Xunit;

namespace CaeriusNet.Tests.Caching;

public sealed class RedisRemoteCacheManagerRegistrationTests
{
    [Fact]
    public async Task RegistrationForwardsTheEntireTagGenerationProtocol()
    {
        var manager = new TagAwareRedisManager();
        ICaeriusRemoteCacheManager registration = new RedisRemoteCacheManagerRegistration(manager);
        var codec = new Int32Codec();
        IReadOnlyList<string> tags = ["tenant:42", "orders:42"];

        var snapshot = await registration.CaptureTagSnapshotAsync(tags, TestContext.Current.CancellationToken);
        var lookup = await registration.TryGetAsync(
            "caerius:v2:payload",
            tags,
            codec,
            TestContext.Current.CancellationToken);
        var published = await registration.SetIfTagSnapshotUnchangedAsync(
            "caerius:v2:payload",
            42,
            tags,
            snapshot,
            codec,
            TimeSpan.FromMinutes(1),
            TestContext.Current.CancellationToken);
        await registration.InvalidateTagsAsync(tags, TestContext.Current.CancellationToken);

        Assert.Equal(ImmutableArray.Create(7L, 11L), snapshot.Generations);
        Assert.True(lookup.Found);
        Assert.Equal(42, lookup.Value);
        Assert.True(published);
        Assert.Equal(1, manager.CaptureCalls);
        Assert.Equal(1, manager.TaggedLookupCalls);
        Assert.Equal(1, manager.ConditionalSetCalls);
        Assert.Equal(1, manager.InvalidateCalls);
        Assert.Equal(tags, manager.LastTags);
    }

    private sealed class TagAwareRedisManager : IRedisCacheManager
    {
        internal int CaptureCalls { get; private set; }
        internal int TaggedLookupCalls { get; private set; }
        internal int ConditionalSetCalls { get; private set; }
        internal int InvalidateCalls { get; private set; }
        internal IReadOnlyList<string>? LastTags { get; private set; }

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
            return ValueTask.CompletedTask;
        }

        public ValueTask<CaeriusCacheTagSnapshot> CaptureTagSnapshotAsync(
            IReadOnlyList<string> tags,
            CancellationToken cancellationToken = default)
        {
            CaptureCalls++;
            LastTags = tags;
            return ValueTask.FromResult(new CaeriusCacheTagSnapshot(ImmutableArray.Create(7L, 11L)));
        }

        public ValueTask<CaeriusCacheLookup<T>> TryGetAsync<T>(
            string key,
            IReadOnlyList<string> tags,
            ICaeriusCacheCodec<T> codec,
            CancellationToken cancellationToken = default)
        {
            TaggedLookupCalls++;
            LastTags = tags;
            return ValueTask.FromResult(CaeriusCacheLookup<T>.Hit((T)(object)42));
        }

        public ValueTask<bool> SetIfTagSnapshotUnchangedAsync<T>(
            string key,
            T value,
            IReadOnlyList<string> tags,
            CaeriusCacheTagSnapshot snapshot,
            ICaeriusCacheCodec<T> codec,
            TimeSpan? expiration,
            CancellationToken cancellationToken = default) where T : notnull
        {
            ConditionalSetCalls++;
            LastTags = tags;
            Assert.Equal(ImmutableArray.Create(7L, 11L), snapshot.Generations);
            var typed = Assert.IsType<int>(value);
            Assert.Equal(42, typed);
            return ValueTask.FromResult(true);
        }

        public ValueTask InvalidateTagsAsync(
            IReadOnlyList<string> tags,
            CancellationToken cancellationToken = default)
        {
            InvalidateCalls++;
            LastTags = tags;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Int32Codec : ICaeriusCacheCodec<int>
    {
        public string CodecId => "tests/int32";
        public string ContractHash => "v1";

        public void Encode(IBufferWriter<byte> writer, int value)
        {
            new CaeriusBinaryWriter(writer).WriteInt32(value);
        }

        public int Decode(ReadOnlySpan<byte> payload)
        {
            return new CaeriusBinaryReader(payload).ReadInt32();
        }
    }
}
