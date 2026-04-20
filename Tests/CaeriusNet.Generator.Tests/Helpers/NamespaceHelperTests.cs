namespace CaeriusNet.Generator.Tests.Helpers;

/// <summary>
///     Direct unit tests for <see cref="NamespaceHelper" />. The generator project exposes its
///     internals to this test assembly via <c>InternalsVisibleTo</c>, so we call <c>GetNamespace</c>
///     directly on real <see cref="INamedTypeSymbol" /> instances built from a tiny in-memory
///     compilation. Catches regressions without spinning up a full generator.
/// </summary>
public sealed class NamespaceHelperTests
{
    private static INamedTypeSymbol GetTypeSymbol(string source, string metadataName)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "Test",
            new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation.GetTypeByMetadataName(metadataName)
               ?? throw new InvalidOperationException($"Type '{metadataName}' not found.");
    }

    [Fact]
    public void Returns_DisplayString_For_Nested_Namespace()
    {
        var symbol = GetTypeSymbol(
            "namespace Foo.Bar.Baz { public sealed class Widget { } }",
            "Foo.Bar.Baz.Widget");

        Assert.Equal("Foo.Bar.Baz", NamespaceHelper.GetNamespace(symbol));
    }

    [Fact]
    public void Returns_Empty_String_For_Global_Namespace()
    {
        var symbol = GetTypeSymbol(
            "public sealed class Widget { }",
            "Widget");

        Assert.Equal(string.Empty, NamespaceHelper.GetNamespace(symbol));
    }

    [Fact]
    public void Returns_Single_Segment_For_Top_Level_Namespace()
    {
        var symbol = GetTypeSymbol(
            "namespace Foo { public sealed class Widget { } }",
            "Foo.Widget");

        Assert.Equal("Foo", NamespaceHelper.GetNamespace(symbol));
    }
}