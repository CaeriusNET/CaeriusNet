using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace CaeriusNet.Tests.Caches;

/// <summary>
///     Unit tests for <see cref="InMemoryCacheManager" />.
///     Each test uses a GUID-based key to avoid interference from the shared static MemoryCache.
/// </summary>
[Collection(FrozenCacheStateCollection.Name)]
public sealed class InMemoryCacheManagerTests
{
    private static readonly TimeSpan LongExpiry = TimeSpan.FromHours(1);

    private static MemoryCacheOptions DefaultOptions()
    {
        return new MemoryCacheOptions
        {
            SizeLimit = null,
            CompactionPercentage = 0.05,
            ExpirationScanFrequency = TimeSpan.FromMinutes(2),
            TrackLinkedCacheEntries = false
        };
    }

    private static MemoryCacheOptions SizeLimitedOptions(long sizeLimit)
    {
        return new MemoryCacheOptions
        {
            SizeLimit = sizeLimit,
            CompactionPercentage = 0.05,
            ExpirationScanFrequency = TimeSpan.FromMinutes(2),
            TrackLinkedCacheEntries = false
        };
    }

    [Fact]
    public void TryGet_MissingKey_Returns_False()
    {
        var key = $"memory_missing_{Guid.NewGuid()}";

        var found = InMemoryCacheManager.TryGet<int>(key, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGet_MissingKey_Outputs_DefaultValue()
    {
        var key = $"memory_default_{Guid.NewGuid()}";

        InMemoryCacheManager.TryGet<int>(key, out var value);

        Assert.Equal(default, value);
    }

    [Fact]
    public void Store_Then_TryGet_Returns_True()
    {
        var key = $"memory_store_{Guid.NewGuid()}";

        InMemoryCacheManager.Store(key, 42, LongExpiry);
        var found = InMemoryCacheManager.TryGet<int>(key, out _);

        Assert.True(found);
    }

    [Fact]
    public void Store_Then_TryGet_Returns_CorrectValue()
    {
        var key = $"memory_value_{Guid.NewGuid()}";
        const int expected = 55;

        InMemoryCacheManager.Store(key, expected, LongExpiry);
        InMemoryCacheManager.TryGet<int>(key, out var value);

        Assert.Equal(expected, value);
    }

    [Fact]
    public void Store_StringValue_TryGet_Returns_CorrectString()
    {
        var key = $"memory_string_{Guid.NewGuid()}";
        const string expected = "hello_memory_cache";

        InMemoryCacheManager.Store(key, expected, LongExpiry);
        InMemoryCacheManager.TryGet<string>(key, out var value);

        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGet_WrongType_Returns_False()
    {
        var key = $"memory_wrongtype_{Guid.NewGuid()}";

        InMemoryCacheManager.Store(key, 42, LongExpiry); // stored as int
        var found = InMemoryCacheManager.TryGet<string>(key, out var value); // retrieved as string

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Store_OverwritesSameKey_WithNewValue()
    {
        var key = $"memory_overwrite_{Guid.NewGuid()}";

        InMemoryCacheManager.Store(key, "first", LongExpiry);
        InMemoryCacheManager.Store(key, "second", LongExpiry); // overwrites — unlike FrozenCacheManager
        InMemoryCacheManager.TryGet<string>(key, out var value);

        Assert.Equal("second", value);
    }

    [Fact]
    public void Store_ListValue_ThenTryGet_Returns_SameList()
    {
        var key = $"memory_list_{Guid.NewGuid()}";
        var list = new List<int> { 1, 2, 3 };

        InMemoryCacheManager.Store(key, list, LongExpiry);
        InMemoryCacheManager.TryGet<List<int>>(key, out var value);

        Assert.Equal(list, value);
    }

    [Fact]
    public void Configure_WithSizeLimit_StoresEntryWithSize()
    {
        var key = $"memory_sizelimit_{Guid.NewGuid()}";

        try
        {
            InMemoryCacheManager.Configure(SizeLimitedOptions(1));

            InMemoryCacheManager.Store(key, "limited", LongExpiry);
            var found = InMemoryCacheManager.TryGet<string>(key, out var value);

            Assert.True(found);
            Assert.Equal("limited", value);
        }
        finally
        {
            InMemoryCacheManager.Configure(DefaultOptions());
        }
    }

    [Fact]
    public async Task Configure_ConcurrentWithStoreAndTryGet_DoesNotThrow()
    {
        var errors = new ConcurrentQueue<Exception>();
        var prefix = $"memory_concurrent_{Guid.NewGuid():N}";

        try
        {
            var configureTask = Task.Run(() =>
            {
                try
                {
                    for (var i = 0; i < 250; i++)
                        InMemoryCacheManager.Configure(i % 2 == 0
                            ? SizeLimitedOptions(4096)
                            : DefaultOptions());
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            });

            var cacheTasks = Enumerable.Range(0, 4)
                .Select(worker => Task.Run(() =>
                {
                    try
                    {
                        for (var i = 0; i < 500; i++)
                        {
                            var key = $"{prefix}_{worker}_{i}";
                            InMemoryCacheManager.Store(key, i, LongExpiry);
                            InMemoryCacheManager.TryGet<int>(key, out _);
                            InMemoryCacheManager.Remove(key);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                }))
                .Append(configureTask)
                .ToArray();

            await Task.WhenAll(cacheTasks);

            Assert.Empty(errors);
        }
        finally
        {
            InMemoryCacheManager.Configure(DefaultOptions());
        }
    }
}
