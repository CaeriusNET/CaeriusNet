namespace CaeriusNet.Tests.Caches;

/// <summary>
///     Unit tests for <see cref="InMemoryCacheManager" />.
///     Each test uses a GUID-based key to avoid interference from the shared static MemoryCache.
/// </summary>
public sealed class InMemoryCacheManagerTests
{
    private static readonly TimeSpan LongExpiry = TimeSpan.FromHours(1);

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
}