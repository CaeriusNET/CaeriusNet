using CaeriusNet.Generator.MultiResults;

namespace CaeriusNet.Generator.Tests.MultiResults;

public sealed class MultiResultSetSignaturesSourceGeneratorTests
{
    [Fact]
    public void RuntimeAssembly_Generates_Multi_Result_Signatures()
    {
        var result = SourceGeneratorTestHelper.RunGeneratorForAssembly<MultiResultSetSignaturesSourceGenerator>(
            "namespace CaeriusNet;",
            "CaeriusNet");

        Assert.Single(result.GeneratedTrees);

        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("public static class GeneratedMultiIEnumerableReadSqlAsyncCommands", generated);
        Assert.Contains("public static class GeneratedMultiReadOnlyCollectionReadSqlAsyncCommands", generated);
        Assert.Contains("public static class GeneratedMultiImmutableArrayReadSqlAsyncCommands", generated);
        Assert.Contains(
            "public ValueTask<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>)>",
            generated);
        Assert.Contains(
            "public ValueTask<(ReadOnlyCollection<TResultSet1>, ReadOnlyCollection<TResultSet2>, ReadOnlyCollection<TResultSet3>, ReadOnlyCollection<TResultSet4>)>",
            generated);
        Assert.Contains(
            "public ValueTask<(ImmutableArray<TResultSet1>, ImmutableArray<TResultSet2>, ImmutableArray<TResultSet3>, ImmutableArray<TResultSet4>, ImmutableArray<TResultSet5>)>",
            generated);
        Assert.Contains("QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3>", generated);
        Assert.Contains("QueryMultipleReadOnlyCollectionAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>",
            generated);
        Assert.Contains(
            "QueryMultipleImmutableArrayAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>",
            generated);
        Assert.Contains("InstrumentMultiResultSetAsync<(", generated);
        Assert.DoesNotContain("public async Task<(", generated);
        Assert.DoesNotContain("QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2>(", generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ConsumerAssembly_Does_Not_Generate_Runtime_Extensions()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<MultiResultSetSignaturesSourceGenerator>(
            "namespace Consumer;");

        Assert.Empty(result.GeneratedTrees);
    }
}
