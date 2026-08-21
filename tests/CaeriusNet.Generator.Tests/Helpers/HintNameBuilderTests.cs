namespace CaeriusNet.Generator.Tests.Helpers;

public sealed class HintNameBuilderTests
{
    [Fact]
    public void Build_Encodes_Escaped_CSharp_Identifiers_For_Roslyn_HintNames()
    {
        var hintName = HintNameBuilder.Build("@event", "café", "Dto");

        Assert.DoesNotContain('@', hintName);
        Assert.DoesNotContain('é', hintName);
        Assert.EndsWith(".Dto.g.cs", hintName, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_Keeps_Ordinary_HintNames_Readable_And_Stable()
    {
        Assert.Equal(
            "Test.Models.Customer.Dto.g.cs",
            HintNameBuilder.Build("Test.Models", "Customer", "Dto"));
    }
}
