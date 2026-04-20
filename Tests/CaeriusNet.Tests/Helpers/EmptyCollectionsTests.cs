using System.Collections.ObjectModel;

namespace CaeriusNet.Tests.Helpers;

/// <summary>
///     <see cref="CaeriusNet.Helpers.EmptyCollections" /> is internal — these tests reach it via reflection
///     to verify the singleton contract. Doing this once here documents the intent for future readers.
/// </summary>
public sealed class EmptyCollectionsTests
{
    private static MethodInfo GetGenericMethod()
    {
        var type = typeof(StoredProcedureParameters).Assembly
            .GetType("CaeriusNet.Helpers.EmptyCollections", true)!;
        return type.GetMethod("ReadOnlyCollection", BindingFlags.Public | BindingFlags.Static)!;
    }

    [Fact]
    public void ReadOnlyCollection_Of_T_Returns_Singleton_Per_Type()
    {
        var method = GetGenericMethod().MakeGenericMethod(typeof(int));

        var first = (ReadOnlyCollection<int>)method.Invoke(null, null)!;
        var second = (ReadOnlyCollection<int>)method.Invoke(null, null)!;

        Assert.NotNull(first);
        Assert.Empty(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void ReadOnlyCollection_Distinct_Singletons_Per_Type_Argument()
    {
        var intMethod = GetGenericMethod().MakeGenericMethod(typeof(int));
        var stringMethod = GetGenericMethod().MakeGenericMethod(typeof(string));

        var ints = (ReadOnlyCollection<int>)intMethod.Invoke(null, null)!;
        var strings = (ReadOnlyCollection<string>)stringMethod.Invoke(null, null)!;

        Assert.Empty(ints);
        Assert.Empty(strings);
        Assert.NotSame(ints, strings);
    }

    [Fact]
    public void ReadOnlyCollection_Singleton_Is_Truly_ReadOnly()
    {
        var method = GetGenericMethod().MakeGenericMethod(typeof(int));
        var collection = (ReadOnlyCollection<int>)method.Invoke(null, null)!;

        // ReadOnlyCollection<T> is immutable by design — adding via the IList interface throws.
        Assert.Throws<NotSupportedException>(() => ((IList<int>)collection).Add(1));
    }
}