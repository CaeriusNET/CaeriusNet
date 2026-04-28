namespace CaeriusNet.Tests.Caches;

/// <summary>
///     Unit tests for <see cref="FrozenCacheManager" />.
///     Each test uses a GUID-based key to avoid interference from the shared static cache state.
///     Placed in <see cref="FrozenCacheStateCollection" /> to serialise execution against other
///     test classes that call <c>FrozenCacheManager.Clear()</c>.
/// </summary>
[Collection(FrozenCacheStateCollection.Name)]
public sealed class FrozenCacheManagerTests
{
    [Fact]
    public void TryGet_MissingKey_Returns_False()
    {
        var key = $"frozen_missing_{Guid.NewGuid()}";

        var found = FrozenCacheManager.TryGet<int>(key, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGet_MissingKey_Outputs_DefaultValue()
    {
        var key = $"frozen_default_{Guid.NewGuid()}";

        FrozenCacheManager.TryGet<int>(key, out var value);

        Assert.Equal(default, value);
    }

    [Fact]
    public void Store_Then_TryGet_Returns_True()
    {
        var key = $"frozen_store_{Guid.NewGuid()}";

        FrozenCacheManager.Store(key, 42);
        var found = FrozenCacheManager.TryGet<int>(key, out _);

        Assert.True(found);
    }

    [Fact]
    public void Store_Then_TryGet_Returns_CorrectValue()
    {
        var key = $"frozen_value_{Guid.NewGuid()}";
        const int expected = 99;

        FrozenCacheManager.Store(key, expected);
        FrozenCacheManager.TryGet<int>(key, out var value);

        Assert.Equal(expected, value);
    }

    [Fact]
    public void Store_SameKey_Twice_IsIdempotent_FirstValuePreserved()
    {
        var key = $"frozen_idempotent_{Guid.NewGuid()}";

        FrozenCacheManager.Store(key, 100);
        FrozenCacheManager.Store(key, 999); // second store is a no-op (double-checked locking)

        FrozenCacheManager.TryGet<int>(key, out var value);

        Assert.Equal(100, value);
    }

    [Fact]
    public void TryGet_WrongType_Returns_False()
    {
        var key = $"frozen_wrongtype_{Guid.NewGuid()}";

        FrozenCacheManager.Store(key, 42); // stored as int
        var found = FrozenCacheManager.TryGet<string>(key, out var value); // retrieved as string

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Store_StringValue_TryGet_Returns_CorrectString()
    {
        var key = $"frozen_string_{Guid.NewGuid()}";
        const string expected = "hello_frozen_cache";

        FrozenCacheManager.Store(key, expected);
        FrozenCacheManager.TryGet<string>(key, out var value);

        Assert.Equal(expected, value);
    }

    [Fact]
    public void StoreRange_Then_TryGet_AllValues_Found()
    {
        var key1 = $"frozen_range_a_{Guid.NewGuid()}";
        var key2 = $"frozen_range_b_{Guid.NewGuid()}";
        var key3 = $"frozen_range_c_{Guid.NewGuid()}";

        var entries = new Dictionary<string, string>
        {
            [key1] = "value1",
            [key2] = "value2",
            [key3] = "value3"
        };

        FrozenCacheManager.StoreRange<string>(entries);

        Assert.True(FrozenCacheManager.TryGet<string>(key1, out var v1));
        Assert.True(FrozenCacheManager.TryGet<string>(key2, out var v2));
        Assert.True(FrozenCacheManager.TryGet<string>(key3, out var v3));
        Assert.Equal("value1", v1);
        Assert.Equal("value2", v2);
        Assert.Equal("value3", v3);
    }

    [Fact]
    public void StoreRange_EmptyCollection_DoesNotThrow()
    {
        var entries = new Dictionary<string, int>();

        var exception = Record.Exception(() => FrozenCacheManager.StoreRange(entries));

        Assert.Null(exception);
    }

    [Fact]
    public void StoreRange_SingleEntry_CanBeRetrieved()
    {
        var key = $"frozen_single_range_{Guid.NewGuid()}";
        var entries = new Dictionary<string, int> { [key] = 777 };

        FrozenCacheManager.StoreRange(entries);
        FrozenCacheManager.TryGet<int>(key, out var value);

        Assert.Equal(777, value);
    }
}
