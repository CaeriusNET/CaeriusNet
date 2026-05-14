using CaeriusNet.Commands.Reads;

namespace CaeriusNet.Tests.Commands;

public sealed class MultiResultApiSurfaceTests
{
    private static readonly Type ValueTaskGeneric = typeof(ValueTask<>);

    public static TheoryData<Type, string, int> MultiResultMethods()
    {
        return new TheoryData<Type, string, int>
        {
            { typeof(MultiIEnumerableReadSqlAsyncCommands), "QueryMultipleIEnumerableAsync", 2 },
            { typeof(MultiReadOnlyCollectionReadSqlAsyncCommands), "QueryMultipleReadOnlyCollectionAsync", 2 },
            { typeof(MultiImmutableArrayReadSqlAsyncCommands), "QueryMultipleImmutableArrayAsync", 2 },
            { typeof(GeneratedMultiIEnumerableReadSqlAsyncCommands), "QueryMultipleIEnumerableAsync", 3 },
            { typeof(GeneratedMultiIEnumerableReadSqlAsyncCommands), "QueryMultipleIEnumerableAsync", 4 },
            { typeof(GeneratedMultiIEnumerableReadSqlAsyncCommands), "QueryMultipleIEnumerableAsync", 5 },
            { typeof(GeneratedMultiReadOnlyCollectionReadSqlAsyncCommands), "QueryMultipleReadOnlyCollectionAsync", 3 },
            { typeof(GeneratedMultiReadOnlyCollectionReadSqlAsyncCommands), "QueryMultipleReadOnlyCollectionAsync", 4 },
            { typeof(GeneratedMultiReadOnlyCollectionReadSqlAsyncCommands), "QueryMultipleReadOnlyCollectionAsync", 5 },
            { typeof(GeneratedMultiImmutableArrayReadSqlAsyncCommands), "QueryMultipleImmutableArrayAsync", 3 },
            { typeof(GeneratedMultiImmutableArrayReadSqlAsyncCommands), "QueryMultipleImmutableArrayAsync", 4 },
            { typeof(GeneratedMultiImmutableArrayReadSqlAsyncCommands), "QueryMultipleImmutableArrayAsync", 5 }
        };
    }

    [Theory]
    [MemberData(nameof(MultiResultMethods))]
    public void Multi_Result_Methods_Return_ValueTask(Type declaringType, string methodName, int genericArity)
    {
        var method = declaringType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == methodName && method.GetGenericArguments().Length == genericArity);

        Assert.True(method.ReturnType.IsGenericType);
        Assert.Equal(ValueTaskGeneric, method.ReturnType.GetGenericTypeDefinition());
    }
}
