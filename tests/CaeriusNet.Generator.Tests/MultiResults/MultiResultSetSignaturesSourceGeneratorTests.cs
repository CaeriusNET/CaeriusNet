using CaeriusNet.MultiResults.Generator;

namespace CaeriusNet.Generator.Tests.MultiResults;

public sealed class MultiResultSetSignaturesSourceGeneratorTests
{
    [Fact]
    public void BuildOnlyGenerator_Emits_MultiResultApiFamilies()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<MultiResultSetApiGenerator>("namespace Consumer;");

        Assert.Single(result.GeneratedTrees);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("public static class CaeriusNetMultiResultSetExtensions", generated);
        Assert.Contains(
            "ValueTask<(global::System.Collections.Generic.IEnumerable<T1> Set1, global::System.Collections.Generic.IEnumerable<T2> Set2, global::System.Collections.Generic.IEnumerable<T3> Set3)>",
            generated);
        Assert.Contains(
            "ValueTask<(global::System.Collections.ObjectModel.ReadOnlyCollection<T1> Set1, global::System.Collections.ObjectModel.ReadOnlyCollection<T2> Set2, global::System.Collections.ObjectModel.ReadOnlyCollection<T3> Set3, global::System.Collections.ObjectModel.ReadOnlyCollection<T4> Set4)>",
            generated);
        Assert.Contains(
            "ValueTask<(global::System.Collections.Immutable.ImmutableArray<T1> Set1, global::System.Collections.Immutable.ImmutableArray<T2> Set2, global::System.Collections.Immutable.ImmutableArray<T3> Set3, global::System.Collections.Immutable.ImmutableArray<T4> Set4, global::System.Collections.Immutable.ImmutableArray<T5> Set5)>",
            generated);
        Assert.Contains("QueryMultipleAsync<T1, T2, T3>", generated);
        Assert.Contains("QueryMultipleReadOnlyCollectionAsync<T1, T2, T3, T4>",
            generated);
        Assert.Contains(
            "QueryMultipleImmutableArrayAsync<T1, T2, T3, T4, T5>",
            generated);
        Assert.Contains("QueryMultipleWithOutputsAsync<T1, T2>", generated);
        Assert.Contains("this global::CaeriusNet.Abstractions.ICaeriusNetTransaction transaction", generated);
        Assert.Contains("private static class EnumerablePlan2<T1, T2>", generated);
        Assert.Contains("internal static readonly global::CaeriusNet.Execution.MultiResultSetPlan Value", generated);
        Assert.Contains("MultiResultSetCapacities.Create10(", generated);
        Assert.DoesNotContain("new int[] {", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Join(\"|\"", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Compilation.AssemblyName", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOnlyGenerator_DoesNotDependOnTheConsumerAssemblyName()
    {
        var result = SourceGeneratorTestHelper.RunGeneratorForAssembly<MultiResultSetApiGenerator>(
            "namespace Consumer;",
            "DifferentAssembly");

        Assert.Single(result.GeneratedTrees);
    }
}
