namespace CaeriusNet.Tests.Helpers;

/// <summary>
///     Validates the internal <c>CacheHelper</c> via reflection. This is the central dispatcher used by
///     every read-side command, so even though the surface is internal we want explicit coverage of:
///     no cache configured → no-op, Frozen/InMemory store-then-retrieve, missing key returns false.
///     Placed in <see cref="FrozenCacheStateCollection" /> to serialise execution against other tests
///     that call <c>FrozenCacheManager.Clear()</c> and would otherwise race with frozen-cache round-trips.
/// </summary>
[Collection(FrozenCacheStateCollection.Name)]
public sealed class CacheHelperTests
{
    private static (MethodInfo TryRetrieve, MethodInfo Store) Methods()
    {
        var asm = typeof(StoredProcedureParameters).Assembly;
        var helper = asm.GetType("CaeriusNet.Helpers.CacheHelper", true)!;
        var tryRetrieve = helper.GetMethod("TryRetrieveFromCache",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var store = helper.GetMethod("StoreInCache",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (tryRetrieve, store);
    }

    private static StoredProcedureParameters MakeSp(string? key, CacheType? type, TimeSpan? ttl = null)
    {
        return new StoredProcedureParameters(
            "dbo",
            "sp_test",
            0,
            Array.Empty<SqlParameter>(),
            key,
            ttl,
            type);
    }

    private static bool TryRetrieve<T>(StoredProcedureParameters sp, out T? value)
    {
        var (tryRetrieve, _) = Methods();
        var generic = tryRetrieve.MakeGenericMethod(typeof(T));
        var args = new object?[] { sp, null, null };
        var found = (bool)generic.Invoke(null, args)!;
        value = (T?)args[2];
        return found;
    }

    private static void Store<T>(StoredProcedureParameters sp, T value)
        where T : notnull
    {
        var (_, store) = Methods();
        var generic = store.MakeGenericMethod(typeof(T));
        generic.Invoke(null, new object?[] { sp, null, value });
    }

    [Fact]
    public void TryRetrieve_Returns_False_When_CacheType_Is_Null()
    {
        var sp = MakeSp("k1", null);
        Assert.False(TryRetrieve<string>(sp, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryRetrieve_Returns_False_When_CacheKey_Is_Empty()
    {
        var sp = MakeSp(string.Empty, CacheType.InMemory);
        Assert.False(TryRetrieve<string>(sp, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Store_Then_Retrieve_RoundTrips_For_InMemory()
    {
        var sp = MakeSp(
            $"caeriushelper-inmem-{Guid.NewGuid():N}",
            CacheType.InMemory,
            TimeSpan.FromMinutes(5));

        Store(sp, "hello");

        Assert.True(TryRetrieve<string>(sp, out var value));
        Assert.Equal("hello", value);
    }

    [Fact]
    public void Store_Then_Retrieve_RoundTrips_For_Frozen()
    {
        var sp = MakeSp(
            $"caeriushelper-frozen-{Guid.NewGuid():N}",
            CacheType.Frozen);

        Store(sp, 42);

        Assert.True(TryRetrieve<int>(sp, out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void Store_Is_NoOp_When_Cache_Not_Configured()
    {
        var sp = MakeSp(null, null);
        // Should not throw and should not allocate / mutate state.
        Store(sp, "ignored");
        Assert.False(TryRetrieve<string>(sp, out _));
    }
}